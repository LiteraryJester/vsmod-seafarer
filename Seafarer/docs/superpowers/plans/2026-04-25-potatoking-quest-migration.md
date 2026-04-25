# Potato King Quest Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the Potato King leg of the Morgan-mine chain into the ProgressionFramework quest system and retarget the in-quest map at the nearest abandoned-farm structure (where the schematic already places a potato seed).

**Architecture:** Standalone server-scope quest `seafarer:potatoking-lastpotato` under "Rebuilding Tortuga". Quest start is implicit on letter delivery; objective is delivering `seafarer:seeds-potato` back to the Potato King; contract handoff stays in dialog (`giveitemstack`) so the narrative line and reward fire together. Map item flips its `schematiccode` from `potatoking` (the King's house, story structure) to `abandoned-farm` (regular scattered structure) so `ItemOceanLocatorMap.FindFreshStructureLocation` resolves the nearest one.

**Tech Stack:** Vintage Story 1.22.0-rc.7+, ProgressionFramework quest system, JSON5 asset files. No C# changes. Validation: `python3 validate-assets.py` and in-game smoke test.

**Spec:** `Seafarer/docs/superpowers/specs/2026-04-25-potatoking-quest-migration-design.md`

---

## File Inventory

| File | Action | Purpose |
|------|--------|---------|
| `Seafarer/Seafarer/assets/seafarer/config/quests/potatoking-lastpotato.json` | create | Quest definition |
| `Seafarer/Seafarer/assets/seafarer/config/dialogue/potatoking.json` | replace | Dialog migrated to quest triggers |
| `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json` | modify | `schematiccode`, `searchRange`, `randomX/Z` |
| `Seafarer/Seafarer/assets/seafarer/lang/en.json` | modify | Add new quest/dialog keys, update changed strings, remove dead keys |

The csproj does not need touching — assets live under the `assets/seafarer/...` tree and are picked up by Vintage Story's standard mod-asset discovery.

All paths in subsequent tasks are relative to the repo root `vsmod-seafarer/` (where `validate-assets.py` lives).

---

## Task 1: Add and update lang strings

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/lang/en.json`

This task lands the lang keys first so the dialog and quest configs (later tasks) reference strings that already exist. The validator flags missing lang refs, so doing lang first keeps every task ending green.

- [ ] **Step 1: Add the two new quest log keys**

Open `Seafarer/Seafarer/assets/seafarer/lang/en.json`. Find the line:

```json
	"quest-dawnmarie-plantation-desc": "Dawn Marie needs seeds to rebuild the plantation. Bring her ten seeds each of six different crops.",
```

(currently line 1128 — search for `quest-dawnmarie-plantation-desc` if line numbers have shifted).

Insert these two new lines immediately after it, before the blank line:

```json
	"quest-potatoking-lastpotato-title": "The Last Potato",
	"quest-potatoking-lastpotato-desc": "The Potato King won't sign Morgan's contract until someone finds him a potato seed. He's marked an abandoned farm on the map — go dig around in the ruins.",
```

- [ ] **Step 2: Update existing potato-king dialog keys with new wording**

Find this block (currently lines 1135–1140):

```json
	"dialogue-potatoking-potato-quest": "Fine. I'll make you a deal. There's one potato left, ONE!, somewhere in the ruins to the north. The temporal storms scattered everything. Find me that potato and I'll sign whatever contract Morgan wants. Here's a map to where I last saw it.",
	"dialogue-potatoking-have-potato": "I found the last potato!",
	"dialogue-potatoking-remind": "About that potato...",
	"dialogue-potatoking-potato-reminder": "The map I gave you shows the ruins to the north. Find the last potato! It's out there somewhere. Without it, no deal.",
	"dialogue-potatoking-potato-received": "Is that... is that really it? THE potato? Oh glorious day! The Potato King's reign shall resume!",
	"dialogue-potatoking-give-contract": "A deal is a deal. Here's the signed contract for Morgan. Tell her the mine will be operational again. And tell her I want a steady supply of potatoes in return!",
```

Replace it with:

```json
	"dialogue-potatoking-quest-start": "Fine. I'll make you a deal. There's one seed potato left, ONE!, in an abandoned farm out in the ruins. The temporal storms scattered everything. Find me that seed and I'll sign whatever contract Morgan wants. Here's a map.",
	"dialogue-potatoking-have-seed": "I found the last potato seed!",
	"dialogue-potatoking-remind": "About that potato...",
	"dialogue-potatoking-quest-reminder": "The map I gave you points to an abandoned farm. Find that potato seed! Without it, no deal.",
	"dialogue-potatoking-lostmap": "You lost the map?! Bah. Here, another copy. Try to keep this one in one piece.",
	"dialogue-potatoking-seed-received": "Is that... is that really it? A seed potato! Oh glorious day! I'll plant it, propagate it, the kingdom will eat again!",
	"dialogue-potatoking-give-contract": "A deal is a deal. Here's the signed contract for Morgan. Tell her the mine will be operational again. And tell her I expect a steady supply of potatoes once my crop's grown!",
```

This:
- Renames `dialogue-potatoking-potato-quest` → `dialogue-potatoking-quest-start` (new wording).
- Renames `dialogue-potatoking-have-potato` → `dialogue-potatoking-have-seed`.
- Keeps `dialogue-potatoking-remind` unchanged.
- Renames `dialogue-potatoking-potato-reminder` → `dialogue-potatoking-quest-reminder`.
- Adds new `dialogue-potatoking-lostmap`.
- Renames `dialogue-potatoking-potato-received` → `dialogue-potatoking-seed-received`.
- Updates `dialogue-potatoking-give-contract` text.

- [ ] **Step 3: Update the map item description**

Find this line (currently line 792):

```json
	"itemdesc-map-potato": "A hastily drawn map to the ruins where the last potato can be found.",
```

Replace with:

```json
	"itemdesc-map-potato": "A hastily drawn map to an abandoned farm where the last potato seed can be found.",
```

- [ ] **Step 4: Verify lang JSON is still valid**

Run:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
python3 -c "import json5; json5.load(open('Seafarer/Seafarer/assets/seafarer/lang/en.json'))"
```

Expected: no output, exit 0. Any output means a syntax error — fix the trailing comma / quote issue in the lines you edited.

- [ ] **Step 5: Run the asset validator and confirm no new errors**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
python3 validate-assets.py 2>&1 | tail -8
```

Expected: `Errors:   0`. Warning count should be roughly the same as the pre-change baseline (~101). `dialogue-potatoking-goodbye` may show up as unused — that's fine, it was already unused.

- [ ] **Step 6: Stage and check the diff is what you expect**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add -p Seafarer/Seafarer/assets/seafarer/lang/en.json
git diff --cached Seafarer/Seafarer/assets/seafarer/lang/en.json | head -60
```

Stage only the potato-king lang changes. Other unrelated changes already in the working tree should NOT be staged.

(No commit yet — we'll commit at the end of Task 5 along with the rest of the migration so the change is one reviewable unit.)

---

## Task 2: Create the quest config

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/config/quests/potatoking-lastpotato.json`

- [ ] **Step 1: Create the quest config file**

Write the following to `Seafarer/Seafarer/assets/seafarer/config/quests/potatoking-lastpotato.json`:

```json
{
  "code": "potatoking-lastpotato",
  "npc": "potatoking",
  "scope": "server",
  "groupLangKey": "quest-group-rebuilding-tortuga",
  "titleLangKey": "quest-potatoking-lastpotato-title",
  "descriptionLangKey": "quest-potatoking-lastpotato-desc",
  "autoEnable": true,
  "objectives": [
    {
      "code": "seed",
      "type": "delivery",
      "items": [{ "item": "seafarer:seeds-potato", "quantity": 1 }],
      "required": 1
    }
  ],
  "rewards": []
}
```

- [ ] **Step 2: Verify JSON parses**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
python3 -c "import json5; print(json5.load(open('Seafarer/Seafarer/assets/seafarer/config/quests/potatoking-lastpotato.json'))['code'])"
```

Expected output: `potatoking-lastpotato`.

- [ ] **Step 3: Run validator**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
python3 validate-assets.py 2>&1 | tail -8
```

Expected: `Errors:   0`.

- [ ] **Step 4: Stage**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/assets/seafarer/config/quests/potatoking-lastpotato.json
```

---

## Task 3: Update map-potato item

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json`

- [ ] **Step 1: Update the locator props and search range**

Open `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json`. Find this block:

```json
        "searchRange": 10000,
        "locatorPropsbyType": {
            "*": {
                "schematiccode": "potatoking",
                "waypointtext": "location-potato",
                "waypointicon": "x",
                "waypointcolor": [0.6, 0.4, 0.1, 1],
                "randomX": 15,
                "randomZ": 15
            }
        }
```

Replace with:

```json
        "searchRange": 2000,
        "locatorPropsbyType": {
            "*": {
                "schematiccode": "abandoned-farm",
                "waypointtext": "location-potato",
                "waypointicon": "x",
                "waypointcolor": [0.6, 0.4, 0.1, 1],
                "randomX": 8,
                "randomZ": 8
            }
        }
```

The four edited values are: `searchRange`, `schematiccode`, `randomX`, `randomZ`. The waypoint metadata (`waypointtext`, `waypointicon`, `waypointcolor`) is unchanged — `location-potato` ("Last Potato") still reads correctly even though the marker now lands on an abandoned farm; it's the in-fiction goal.

- [ ] **Step 2: Verify JSON parses**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
python3 -c "import json5; d = json5.load(open('Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json')); print(d['attributes']['searchRange'], d['attributes']['locatorPropsbyType']['*']['schematiccode'])"
```

Expected output: `2000 abandoned-farm`.

- [ ] **Step 3: Run validator**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
python3 validate-assets.py 2>&1 | tail -8
```

Expected: `Errors:   0`.

- [ ] **Step 4: Stage**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json
```

---

## Task 4: Rewrite the Potato King dialogue

**Files:**
- Replace: `Seafarer/Seafarer/assets/seafarer/config/dialogue/potatoking.json`

- [ ] **Step 1: Replace the dialogue file with the migrated version**

Replace the entire contents of `Seafarer/Seafarer/assets/seafarer/config/dialogue/potatoking.json` with:

```jsonc
{
    "components": [
        // === First Meeting ===
        {
            "code": "testhasmet",
            "owner": "potatoking",
            "type": "condition",
            "variable": "player.hasmetpotatoking",
            "isNotValue": "true",
            "thenJumpTo": "firstmeet",
            "elseJumpTo": "welcomeback"
        },
        {
            "code": "firstmeet",
            "owner": "potatoking",
            "type": "talk",
            "setVariables": { "player.hasmetpotatoking": "true" },
            "text": [{ "value": "seafarer:dialogue-potatoking-welcome" }],
            "jumpTo": "main"
        },
        {
            "code": "welcomeback",
            "owner": "potatoking",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-potatoking-welcomeback" }],
            "jumpTo": "main"
        },

        // === Main Menu ===
        {
            "code": "main",
            "owner": "player",
            "type": "talk",
            "text": [
                { "value": "dialogue-name", "jumpTo": "name" },

                // Quest not started, player presented Morgan's letter
                {
                    "value": "seafarer:dialogue-potatoking-show-letter",
                    "jumpTo": "lastpotato-letter-received",
                    "conditions": [
                        { "variable": "global.quest-seafarer-potatoking-lastpotato-status", "isNotValue": "active" },
                        { "variable": "global.quest-seafarer-potatoking-lastpotato-status", "isNotValue": "completed" },
                        { "variable": "player.inventory", "isValue": "{type: 'item', code: 'seafarer:letter-morgan', stacksize: 1}" }
                    ]
                },

                // Quest active, player has a seed → deliver
                {
                    "value": "seafarer:dialogue-potatoking-have-seed",
                    "jumpTo": "lastpotato-deliver",
                    "conditions": [
                        { "variable": "global.quest-seafarer-potatoking-lastpotato-status", "isValue": "active" },
                        { "variable": "player.inventory", "isValue": "{type: 'item', code: 'seafarer:seeds-potato', stacksize: 1}" }
                    ]
                },

                // Quest active, no seed → reminder (with lost-map handling)
                {
                    "value": "seafarer:dialogue-potatoking-remind",
                    "jumpTo": "lastpotato-reminder",
                    "conditions": [
                        { "variable": "global.quest-seafarer-potatoking-lastpotato-status", "isValue": "active" }
                    ]
                }
            ]
        },
        {
            "code": "name",
            "owner": "potatoking",
            "type": "talk",
            "trigger": "revealname",
            "text": [{ "value": "seafarer:dialogue-potatoking-name" }],
            "jumpTo": "main"
        },

        // === Letter delivery → quest start (implicit accept) ===
        {
            "code": "lastpotato-letter-received",
            "owner": "potatoking",
            "type": "talk",
            "trigger": "takefrominventory",
            "triggerdata": {
                "type": "item",
                "code": "seafarer:letter-morgan"
            },
            "text": [{ "value": "seafarer:dialogue-potatoking-letter-response" }],
            "jumpTo": "lastpotato-start"
        },
        {
            "code": "lastpotato-start",
            "owner": "potatoking",
            "type": "talk",
            "trigger": "questStart",
            "triggerdata": { "code": "potatoking-lastpotato" },
            "text": [{ "value": "seafarer:dialogue-potatoking-quest-start" }],
            "jumpTo": "lastpotato-give-map"
        },
        {
            "code": "lastpotato-give-map",
            "owner": "potatoking",
            "trigger": "giveitemstack",
            "triggerdata": {
                "type": "item",
                "code": "seafarer:map-potato",
                "stacksize": 1
            },
            "jumpTo": "main"
        },

        // === Reminder / lost-map replacement (mirrors Celeste pattern) ===
        {
            "code": "lastpotato-reminder",
            "owner": "potatoking",
            "type": "condition",
            "variable": "player.inventory",
            "isValue": "{type: 'item', code: 'seafarer:map-potato', stacksize: 1}",
            "thenJumpTo": "lastpotato-reminder-hasmap",
            "elseJumpTo": "lastpotato-reminder-givemap"
        },
        {
            "code": "lastpotato-reminder-hasmap",
            "owner": "potatoking",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-potatoking-quest-reminder" }],
            "jumpTo": "main"
        },
        {
            "code": "lastpotato-reminder-givemap",
            "owner": "potatoking",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-potatoking-lostmap" }],
            "jumpTo": "lastpotato-give-map"
        },

        // === Seed delivery → contract handoff ===
        {
            "code": "lastpotato-deliver",
            "owner": "potatoking",
            "type": "talk",
            "trigger": "questDeliver",
            "triggerdata": { "code": "potatoking-lastpotato", "objective": "seed" },
            "text": [{ "value": "seafarer:dialogue-potatoking-seed-received" }],
            "jumpTo": "lastpotato-give-contract"
        },
        {
            "code": "lastpotato-give-contract",
            "owner": "potatoking",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-potatoking-give-contract" }],
            "trigger": "giveitemstack",
            "triggerdata": {
                "type": "item",
                "code": "seafarer:signed-contract",
                "stacksize": 1
            },
            "jumpTo": "main"
        }
    ]
}
```

- [ ] **Step 2: Verify JSON5 parses**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
python3 -c "import json5; d = json5.load(open('Seafarer/Seafarer/assets/seafarer/config/dialogue/potatoking.json')); print(len(d['components']), 'components')"
```

Expected output: `13 components`.

- [ ] **Step 3: Sanity-check that every dialog text key resolves to a lang entry**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
python3 - <<'PY'
import json5, re
dlg = json5.load(open('Seafarer/Seafarer/assets/seafarer/config/dialogue/potatoking.json'))
lang = json5.load(open('Seafarer/Seafarer/assets/seafarer/lang/en.json'))
def collect(node, out):
    if isinstance(node, dict):
        for k, v in node.items():
            if k == 'value' and isinstance(v, str): out.append(v)
            else: collect(v, out)
    elif isinstance(node, list):
        for i in node: collect(i, out)
keys = []
collect(dlg, keys)
missing = []
for k in keys:
    if k.startswith('seafarer:'):
        if k.split(':', 1)[1] not in lang: missing.append(k)
print('missing:', missing if missing else 'none')
PY
```

Expected output: `missing: none`.

- [ ] **Step 4: Run the full asset validator**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
python3 validate-assets.py 2>&1 | tail -8
```

Expected: `Errors:   0`. Warning count near baseline (~101). If a new lang-related warning appears (e.g. unused `dialogue-potatoking-goodbye`), that's acceptable — it was unused before too. If a NEW error appears, re-read the dialog and lang sections of this plan.

- [ ] **Step 5: Build the mod to confirm asset paths are valid**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -10
```

Expected: `Build succeeded`. Build doesn't load asset JSON, but it confirms the project still compiles unchanged.

- [ ] **Step 6: Stage**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/assets/seafarer/config/dialogue/potatoking.json
```

---

## Task 5: Final validation and commit

**Files:** none — verifies the previous tasks together.

- [ ] **Step 1: Confirm staged set is exactly the four files**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git diff --cached --name-only
```

Expected output (order may vary):

```
Seafarer/Seafarer/assets/seafarer/config/dialogue/potatoking.json
Seafarer/Seafarer/assets/seafarer/config/quests/potatoking-lastpotato.json
Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json
Seafarer/Seafarer/assets/seafarer/lang/en.json
```

If anything else is staged, unstage it: `git reset HEAD <file>`.

- [ ] **Step 2: Run the validator once more on the full staged change**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
python3 validate-assets.py 2>&1 | tail -8
```

Expected: `Errors:   0`.

- [ ] **Step 3: Spot-check that the four removed lang keys are not referenced anywhere**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
grep -rn 'dialogue-potatoking-potato-quest\|dialogue-potatoking-have-potato\|dialogue-potatoking-potato-reminder\|dialogue-potatoking-potato-received' Seafarer/Seafarer/assets/ 2>/dev/null
```

Expected: empty output. If any line comes back, the rename in Task 1 didn't reach a corresponding usage — go fix it.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git commit -m "$(cat <<'EOF'
feat(quest): migrate Potato King to quest system, point map at abandoned-farm

Replaces the entity-flag-based Potato King dialog with a server-scope
ProgressionFramework quest (potatoking-lastpotato) under the Rebuilding
Tortuga group. Letter delivery implicitly starts the quest; objective is
delivering seafarer:seeds-potato to the King; contract handoff stays in
dialog so the line and reward fire together.

Map item map-potato now points at the abandoned-farm structure (regular
scattered) instead of the King's own house. The abandoned-farm schematic
already places a potato seed, so no schematic change is needed.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 5: Confirm clean working tree for these files**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git status --short Seafarer/Seafarer/assets/seafarer/config/quests/potatoking-lastpotato.json Seafarer/Seafarer/assets/seafarer/config/dialogue/potatoking.json Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json Seafarer/Seafarer/assets/seafarer/lang/en.json
```

Expected: empty output (the four files are committed, working tree matches index).

---

## Manual smoke test (post-commit)

This isn't an automatable check; perform it after launching Vintage Story with the mod loaded. The plan is complete once these pass:

1. New world. Talk to Morgan, choose "I'd like to help with the rebuilding effort" → accept the mine quest → Morgan gives `seafarer:letter-morgan`. The morgan-mine quest appears in the Ledger (press `L`) under "Rebuilding Tortuga" as Active.
2. Walk to the Potato King. Choose "I have a letter from Morgan." Letter is consumed; `seafarer:map-potato` is given; `potatoking-lastpotato` quest appears in the Ledger as Active.
3. Right-click the map. A waypoint titled "Last Potato" should appear on the world map within ~2000 blocks.
4. Travel to the waypoint. The abandoned-farm structure should be visible. Search inside it and pick up a `seafarer:seeds-potato` item.
5. Return to the Potato King. Choose "I found the last potato seed!" The seed is consumed; `potatoking-lastpotato` flips to Completed; `seafarer:signed-contract` is given.
6. Return to Morgan. Choose "I have the contract." Contract is consumed; `morgan-mine` flips to Completed; `rebuilding-tier` ticks once (one of four).
7. Lost-map case: in a separate save (or by manually dropping the map), revisit the Potato King while the quest is still Active. Choose "About that potato..." — the King should give a replacement `seafarer:map-potato` with the lost-map line.
