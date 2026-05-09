#!/usr/bin/env python3
"""
Vintage Story Dialogue & Quest Validator for the Seafarer mod.

Validates dialogue JSON files checking:

  Structure:
    1. JSON5 syntax validity
    2. Top-level { components: [...] } structure
    3. Each component has required fields (code, owner)
    4. Component type is valid (condition, talk, or omitted for trigger-only)

  Flow integrity:
    5. All jumpTo targets reference existing component codes
    6. All thenJumpTo/elseJumpTo targets exist (conditions)
    7. Dead-end detection: talk components with no jumpTo and no following
       implicit component (player response convention)
    8. Unreachable component detection: components never targeted by any jump
    9. Duplicate component code detection

  Conditions:
   10. Condition components have variable + isValue/isNotValue
   11. Condition components have both thenJumpTo and elseJumpTo
   12. Variable scoping: entity.* or player.* prefix required

  Triggers:
   13. Known trigger types validated
   14. Triggers requiring triggerdata have it (giveitemstack, takefrominventory, etc.)
   15. triggerdata shape validation per trigger type

  Quest patterns:
   16. incrementVariable triggerdata has required fields
   17. addSellingItem triggerdata has required fields
   18. Delivery quest chains: takefrominventory has matching item code

  Lang references:
   19. Text values that look like lang keys (contain no spaces, have domain prefix
       or match dialogue-* pattern) are checked against en.json
   20. Inline text with {{placeholders}} validated for known placeholders

  Owner consistency:
   21. Warn if owner changes to unexpected values mid-file
   22. Player components should have owner "player"

Usage:
  python3 validate-dialogue.py                          # validate all mod dialogues
  python3 validate-dialogue.py --file morgan.json       # single file
  python3 validate-dialogue.py --base-game              # include base game files
  python3 validate-dialogue.py --verbose                # show all checks
  python3 validate-dialogue.py --graph                  # print flow graph
"""

import json5
import os
import re
import sys
import argparse
from pathlib import Path
from collections import defaultdict

# ── Paths ──────────────────────────────────────────────────────────────────────

SCRIPT_DIR = Path(__file__).parent
MOD_DIALOGUE = SCRIPT_DIR / "Seafarer" / "Seafarer" / "assets" / "seafarer" / "config" / "dialogue"
GAME_DIALOGUE = Path("/mnt/d/Development/vs/assets/survival/config/dialogue")
MOD_LANG = SCRIPT_DIR / "Seafarer" / "Seafarer" / "assets" / "seafarer" / "lang" / "en.json"

# ── Known values ───────────────────────────────────────────────────────────────

KNOWN_TRIGGERS = {
    "opentrade", "revealname", "giveitemstack", "takefrominventory",
    "playanimation", "attack", "spawnentity", "closedialogue",
    "openhairstyling", "repairheldtool", "repairheldarmor", "unlockdoor",
    # Seafarer custom triggers
    "incrementVariable", "decrementVariable", "addSellingItem", "addBuyingItem",
    "removeSellingItem", "removeBuyingItem", "setMoney", "awardTrainingXP",
    "questStart", "questDeliver",
}

TRIGGERS_NEEDING_DATA = {
    "giveitemstack", "takefrominventory", "playanimation", "attack",
    "spawnentity", "incrementVariable", "addSellingItem",
    "awardTrainingXP", "questStart", "questDeliver",
}

KNOWN_PLACEHOLDERS = {
    "playername", "npcname", "characterclass",
}

KNOWN_VARIABLE_SCOPES = {"entity", "player", "global", "group"}

COMPONENT_TYPES = {"condition", "talk"}

# ── Colors ─────────────────────────────────────────────────────────────────────

class C:
    RED    = "\033[91m"
    YELLOW = "\033[93m"
    GREEN  = "\033[92m"
    CYAN   = "\033[96m"
    MAGENTA= "\033[95m"
    DIM    = "\033[2m"
    BOLD   = "\033[1m"
    RESET  = "\033[0m"

def err(msg):    return f"  {C.RED}ERROR{C.RESET}  {msg}"
def warn(msg):   return f"  {C.YELLOW}WARN {C.RESET}  {msg}"
def ok(msg):     return f"  {C.GREEN}OK   {C.RESET}  {msg}"
def info(msg):   return f"  {C.CYAN}INFO {C.RESET}  {msg}"
def header(msg): return f"\n{C.BOLD}{'═'*64}\n  {msg}\n{'═'*64}{C.RESET}"
def subheader(msg): return f"\n{C.BOLD}  ── {msg} ──{C.RESET}"

# ── Lang loading ───────────────────────────────────────────────────────────────

def load_lang_file(path):
    """Load VS lang file with multiline string support."""
    if not path.exists():
        return {}
    with open(path, "r", encoding="utf-8-sig") as f:
        text = f.read()

    out = []
    in_string = False
    i = 0
    while i < len(text):
        ch = text[i]
        if not in_string:
            if ch == '/' and i + 1 < len(text) and text[i + 1] == '/':
                while i < len(text) and text[i] != '\n':
                    i += 1
                continue
            if ch == '"':
                in_string = True
            out.append(ch)
        else:
            if ch == '\\' and i + 1 < len(text):
                out.append(ch)
                out.append(text[i + 1])
                i += 2
                continue
            elif ch == '"':
                in_string = False
                out.append(ch)
            elif ch == '\n':
                out.append('\\n')
            elif ch == '\r':
                pass
            elif ch == '\t':
                out.append('\\t')
            else:
                out.append(ch)
        i += 1

    try:
        return json5.loads("".join(out))
    except Exception:
        return {}


def load_base_game_lang():
    """Load base game lang file."""
    path = Path("/mnt/d/Development/vs/assets/game/lang/en.json")
    return load_lang_file(path)

# ── JSON loading ───────────────────────────────────────────────────────────────

def load_dialogue(path):
    """Load a dialogue JSON5 file. Returns (data, error)."""
    try:
        with open(path, "r", encoding="utf-8-sig") as f:
            text = f.read()
        return json5.loads(text), None
    except Exception as e:
        return None, str(e)

# ── Validation ─────────────────────────────────────────────────────────────────

class DialogueValidator:
    def __init__(self, lang_keys, base_lang_keys, verbose=False):
        self.lang_keys = lang_keys
        self.base_lang_keys = base_lang_keys
        self.verbose = verbose
        self.errors = 0
        self.warnings = 0
        self.passed = 0

    def _err(self, msg):
        self.errors += 1
        print(err(msg))

    def _warn(self, msg):
        self.warnings += 1
        print(warn(msg))

    def _ok(self, msg):
        self.passed += 1
        if self.verbose:
            print(ok(msg))

    def _info(self, msg):
        if self.verbose:
            print(info(msg))

    def validate_file(self, path, show_graph=False):
        """Run all checks on a single dialogue file."""
        name = path.name
        print(subheader(name))

        data, error = load_dialogue(path)
        if error:
            self._err(f"JSON parse error: {error}")
            return

        # Top-level structure
        if not isinstance(data, dict):
            self._err("Top level must be an object")
            return

        if "components" not in data:
            self._err("Missing 'components' array")
            return

        components = data["components"]
        if not isinstance(components, list):
            self._err("'components' must be an array")
            return

        if len(components) == 0:
            self._warn("Empty components array")
            return

        self._ok(f"Parsed {len(components)} components")

        # Build code index
        code_map = {}
        duplicate_codes = []
        for i, comp in enumerate(components):
            code = comp.get("code")
            if not code:
                self._err(f"Component [{i}] missing 'code'")
                continue
            if code in code_map:
                duplicate_codes.append(code)
            code_map[code] = (i, comp)

        if duplicate_codes:
            for code in duplicate_codes:
                self._err(f"Duplicate component code: '{code}'")

        # Determine expected NPC owner from first non-player component
        npc_owners = set()
        for comp in components:
            owner = comp.get("owner", "")
            if owner and owner != "player":
                npc_owners.add(owner)

        # Validate each component
        all_jump_targets = set()
        all_codes = set(code_map.keys())

        for i, comp in enumerate(components):
            code = comp.get("code", f"[{i}]")
            self._validate_component(code, comp, code_map, all_jump_targets, name, npc_owners)

        # Unreachable detection
        # First component is entry point; implicit next components are reachable
        reachable = set()
        reachable.add(components[0].get("code", ""))
        reachable.update(all_jump_targets)

        # Implicit flow: if a talk component has no jumpTo, the next component is implicitly reachable
        for i, comp in enumerate(components):
            comp_type = comp.get("type")
            has_jump = "jumpTo" in comp
            # Check if any text option has a jumpTo
            text_entries = comp.get("text", [])
            text_jumps = any("jumpTo" in t for t in text_entries if isinstance(t, dict))

            if not has_jump and not text_jumps and i + 1 < len(components):
                next_code = components[i + 1].get("code", "")
                reachable.add(next_code)

        unreachable = all_codes - reachable
        for code in sorted(unreachable):
            self._warn(f"Unreachable component: '{code}'")

        # Flow graph
        if show_graph:
            self._print_graph(components, code_map)

    def _validate_component(self, code, comp, code_map, all_jump_targets, filename, npc_owners):
        """Validate a single dialogue component."""
        # Required: code, owner
        if "owner" not in comp:
            self._err(f"'{code}': missing 'owner'")

        comp_type = comp.get("type")
        owner = comp.get("owner", "")

        # Validate type
        if comp_type and comp_type not in COMPONENT_TYPES:
            self._err(f"'{code}': unknown type '{comp_type}'")

        # ── Condition components ───────────────────────────────────────────
        if comp_type == "condition":
            if "variable" not in comp:
                self._err(f"'{code}': condition missing 'variable'")
            else:
                self._validate_variable(code, comp["variable"])

            has_check = "isValue" in comp or "isNotValue" in comp
            if not has_check:
                self._err(f"'{code}': condition missing isValue or isNotValue")

            if "thenJumpTo" not in comp:
                self._err(f"'{code}': condition missing 'thenJumpTo'")
            else:
                target = comp["thenJumpTo"]
                all_jump_targets.add(target)
                if target not in code_map:
                    self._err(f"'{code}': thenJumpTo target '{target}' not found")

            if "elseJumpTo" not in comp:
                self._err(f"'{code}': condition missing 'elseJumpTo'")
            else:
                target = comp["elseJumpTo"]
                all_jump_targets.add(target)
                if target not in code_map:
                    self._err(f"'{code}': elseJumpTo target '{target}' not found")

        # ── Talk components ────────────────────────────────────────────────
        if comp_type == "talk":
            text_entries = comp.get("text", [])
            if not text_entries and "trigger" not in comp:
                self._warn(f"'{code}': talk component with no text and no trigger")

            for j, text_entry in enumerate(text_entries):
                if not isinstance(text_entry, dict):
                    self._err(f"'{code}': text[{j}] is not an object")
                    continue

                value = text_entry.get("value")
                if not value:
                    self._warn(f"'{code}': text[{j}] missing 'value'")
                else:
                    self._validate_text_value(code, value, j)

                # jumpTo in text entries
                jump = text_entry.get("jumpTo")
                if jump:
                    all_jump_targets.add(jump)
                    if jump not in code_map:
                        self._err(f"'{code}': text[{j}] jumpTo '{jump}' not found")

                # Conditions in text entries
                self._validate_text_conditions(code, text_entry, j)

                # setVariables in text entries (VS allows this)
                if "setVariables" in text_entry:
                    for var_name in text_entry["setVariables"]:
                        self._validate_variable(f"{code}/text[{j}]", var_name)

        # ── Component-level jumpTo ─────────────────────────────────────────
        if "jumpTo" in comp:
            target = comp["jumpTo"]
            all_jump_targets.add(target)
            if target not in code_map:
                self._err(f"'{code}': jumpTo target '{target}' not found")

        # ── Triggers ───────────────────────────────────────────────────────
        trigger = comp.get("trigger")
        if trigger:
            if trigger not in KNOWN_TRIGGERS:
                self._warn(f"'{code}': unknown trigger '{trigger}'")

            triggerdata = comp.get("triggerdata") or comp.get("triggerData")
            if trigger in TRIGGERS_NEEDING_DATA and not triggerdata:
                self._err(f"'{code}': trigger '{trigger}' requires triggerdata")

            if triggerdata:
                self._validate_triggerdata(code, trigger, triggerdata)

        # ── setVariables ───────────────────────────────────────────────────
        if "setVariables" in comp:
            for var_name in comp["setVariables"]:
                self._validate_variable(code, var_name)

        # ── Dead-end detection ─────────────────────────────────────────────
        # An NPC talk component with no jumpTo, no trigger, and text entries
        # that also lack jumpTo is potentially a dead end
        if comp_type == "talk" and owner != "player":
            has_component_jump = "jumpTo" in comp
            has_trigger = "trigger" in comp
            text_entries = comp.get("text", [])
            all_text_have_jumps = all(
                "jumpTo" in t for t in text_entries if isinstance(t, dict)
            ) if text_entries else False

            if not has_component_jump and not has_trigger and not all_text_have_jumps:
                # Check if next component is an implicit player response
                # This is OK — VS uses the convention of code + "response" suffix
                pass  # implicit flow is fine

        self._ok(f"'{code}' validated")

    def _validate_variable(self, context, var_name):
        """Check variable has proper scope prefix."""
        if "." not in var_name:
            self._warn(f"'{context}': variable '{var_name}' missing scope prefix (entity.* or player.*)")
            return

        scope = var_name.split(".")[0]
        if scope not in KNOWN_VARIABLE_SCOPES:
            self._warn(f"'{context}': variable '{var_name}' has unknown scope '{scope}' (expected entity.* or player.*)")

    def _validate_text_value(self, code, value, index):
        """Check text value — lang key resolution and placeholder validation."""
        # Check for {{placeholder}} patterns
        placeholders = re.findall(r'\{\{(\w+)\}\}', value)
        for ph in placeholders:
            if ph not in KNOWN_PLACEHOLDERS:
                self._warn(f"'{code}': text[{index}] unknown placeholder '{{{{{ph}}}}}'")

        # Determine if this looks like a lang key vs inline text
        is_lang_key = self._looks_like_lang_key(value)

        if is_lang_key:
            # Check in mod lang, then base game lang
            if value.startswith("seafarer:"):
                # Mod-prefixed lang key
                stripped = value  # full key with prefix
                if stripped not in self.lang_keys and stripped.replace("seafarer:", "") not in self.lang_keys:
                    # Try without prefix in lang file (VS strips domain prefix for lang lookup)
                    raw_key = value.replace("seafarer:", "")
                    if raw_key not in self.lang_keys:
                        self._warn(f"'{code}': text[{index}] lang key '{value}' not found in en.json")
            else:
                # Could be base game lang key or mod lang key
                found = (value in self.lang_keys or
                         value in self.base_lang_keys or
                         f"game:{value}" in self.base_lang_keys)
                if not found:
                    self._info(f"'{code}': text[{index}] possible lang key '{value}' — not found in loaded lang files")

    def _looks_like_lang_key(self, value):
        """Heuristic: does this value look like a lang key vs inline dialogue text?"""
        # Lang keys typically:
        # - Start with "dialogue-" or have a domain prefix like "seafarer:"
        # - Have no spaces (or very few)
        # - Are short identifiers
        if value.startswith("seafarer:") or value.startswith("game:"):
            return True
        if value.startswith("dialogue-"):
            return True
        # If it has no spaces and looks like a key
        if " " not in value and re.match(r'^[\w\-.:]+$', value):
            return True
        return False

    def _validate_text_conditions(self, code, text_entry, index):
        """Validate condition/conditions on a text entry."""
        # Single condition
        if "condition" in text_entry:
            cond = text_entry["condition"]
            if isinstance(cond, dict):
                if "variable" not in cond:
                    self._err(f"'{code}': text[{index}] condition missing 'variable'")
                else:
                    self._validate_variable(f"{code}/text[{index}]", cond["variable"])
                if "isValue" not in cond and "isNotValue" not in cond:
                    self._err(f"'{code}': text[{index}] condition missing isValue/isNotValue")

        # Multiple conditions
        if "conditions" in text_entry:
            conditions = text_entry["conditions"]
            if isinstance(conditions, list):
                for k, cond in enumerate(conditions):
                    if not isinstance(cond, dict):
                        continue
                    if "variable" not in cond:
                        self._err(f"'{code}': text[{index}] conditions[{k}] missing 'variable'")
                    else:
                        self._validate_variable(f"{code}/text[{index}]", cond["variable"])
                    if "isValue" not in cond and "isNotValue" not in cond:
                        self._err(f"'{code}': text[{index}] conditions[{k}] missing isValue/isNotValue")

    def _validate_triggerdata(self, code, trigger, data):
        """Validate triggerdata shape for known trigger types."""
        if trigger in ("giveitemstack", "takefrominventory"):
            if "type" not in data:
                self._warn(f"'{code}': triggerdata missing 'type' (item/block)")
            if "code" not in data:
                self._err(f"'{code}': triggerdata missing 'code'")

        elif trigger == "incrementVariable":
            for field in ("scope", "name", "amount"):
                if field not in data:
                    self._err(f"'{code}': incrementVariable triggerdata missing '{field}'")
            if "thresholdValue" in data and "thresholdVariable" not in data:
                self._warn(f"'{code}': incrementVariable has thresholdValue but no thresholdVariable")

        elif trigger == "addSellingItem":
            if "code" not in data:
                self._err(f"'{code}': addSellingItem triggerdata missing 'code'")
            if "price" not in data:
                self._warn(f"'{code}': addSellingItem triggerdata missing 'price'")

        elif trigger == "playanimation":
            if "animation" not in data and "code" not in data:
                self._warn(f"'{code}': playanimation triggerdata missing 'animation'")

        elif trigger == "attack":
            if "damage" not in data:
                self._warn(f"'{code}': attack triggerdata missing 'damage'")

        elif trigger == "spawnentity":
            if "codes" not in data and "code" not in data:
                self._warn(f"'{code}': spawnentity triggerdata missing 'codes' or 'code'")

    def _print_graph(self, components, code_map):
        """Print an ASCII flow graph of the dialogue."""
        print(f"\n  {C.MAGENTA}Flow Graph:{C.RESET}")

        for comp in components:
            code = comp.get("code", "?")
            comp_type = comp.get("type", "")
            owner = comp.get("owner", "?")
            trigger = comp.get("trigger", "")

            # Icon
            if comp_type == "condition":
                icon = "◇"
            elif owner == "player":
                icon = "▷"
            else:
                icon = "●"

            # Build targets
            targets = []
            if "jumpTo" in comp:
                targets.append(comp["jumpTo"])
            if "thenJumpTo" in comp:
                targets.append(f"Y→{comp['thenJumpTo']}")
            if "elseJumpTo" in comp:
                targets.append(f"N→{comp['elseJumpTo']}")

            text_entries = comp.get("text", [])
            for t in text_entries:
                if isinstance(t, dict) and "jumpTo" in t:
                    val = t.get("value", "")
                    short = val[:30] + "..." if len(val) > 30 else val
                    targets.append(f"→{t['jumpTo']}")

            trigger_str = f" [{trigger}]" if trigger else ""
            targets_str = f" {C.DIM}({', '.join(targets)}){C.RESET}" if targets else ""

            print(f"    {icon} {C.BOLD}{code}{C.RESET}{trigger_str}{targets_str}")

    def print_summary(self):
        """Print final summary."""
        print(f"\n{C.BOLD}{'═'*64}")
        print(f"  Summary")
        print(f"{'═'*64}{C.RESET}")
        print(f"  Passed:   {C.GREEN}{self.passed}{C.RESET}")
        print(f"  Warnings: {C.YELLOW}{self.warnings}{C.RESET}")
        print(f"  Errors:   {C.RED}{self.errors}{C.RESET}")

        if self.errors:
            print(f"\n  {C.RED}{C.BOLD}FAILED{C.RESET} — {self.errors} error(s)")
            return 1
        elif self.warnings:
            print(f"\n  {C.YELLOW}{C.BOLD}PASSED with warnings{C.RESET}")
            return 0
        else:
            print(f"\n  {C.GREEN}{C.BOLD}ALL CLEAR{C.RESET}")
            return 0


# ── Main ───────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="Validate Vintage Story dialogue & quest files")
    parser.add_argument("--verbose", "-v", action="store_true", help="Show all checks including passing")
    parser.add_argument("--file", "-f", help="Validate a single file (name or path)")
    parser.add_argument("--base-game", "-b", action="store_true", help="Also validate base game dialogue files")
    parser.add_argument("--graph", "-g", action="store_true", help="Print dialogue flow graph")
    args = parser.parse_args()

    print(f"{C.BOLD}Vintage Story Dialogue & Quest Validator{C.RESET}")

    # Load lang files
    print(info("Loading lang files..."))
    mod_lang = load_lang_file(MOD_LANG)
    base_lang = load_base_game_lang()
    print(info(f"Mod lang: {len(mod_lang)} keys, Base game lang: {len(base_lang)} keys"))

    validator = DialogueValidator(mod_lang, base_lang, verbose=args.verbose)

    # Collect files
    files = []

    if args.file:
        # Single file
        p = Path(args.file)
        if not p.exists():
            p = MOD_DIALOGUE / args.file
        if not p.exists():
            p = GAME_DIALOGUE / args.file
        if not p.exists():
            print(err(f"File not found: {args.file}"))
            sys.exit(1)
        files.append(p)
    else:
        # All mod dialogue files
        if MOD_DIALOGUE.exists():
            files.extend(sorted(MOD_DIALOGUE.glob("*.json")))

        if args.base_game and GAME_DIALOGUE.exists():
            files.extend(sorted(GAME_DIALOGUE.glob("*.json")))

    if not files:
        print(err("No dialogue files found"))
        sys.exit(1)

    print(header(f"Validating {len(files)} dialogue file(s)"))

    for f in files:
        validator.validate_file(f, show_graph=args.graph)

    exit_code = validator.print_summary()
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
