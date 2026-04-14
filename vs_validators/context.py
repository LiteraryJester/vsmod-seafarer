"""ValidationContext + FoodProfile."""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from vs_validators.common import load_json5
from vs_validators.indexes import (
    build_file_index, load_patches, patches_by_target, load_lang_raw, PatchRef
)


@dataclass
class FoodProfile:
    code: str
    source_file: Path
    foodcategory: str | None = None
    variants: list[str] = field(default_factory=list)
    variant_states: list[str] = field(default_factory=list)
    fields_present: set[str] = field(default_factory=set)
    attributes_present: set[str] = field(default_factory=set)
    food_tags: list[str] = field(default_factory=list)
    raw: dict = field(default_factory=dict)

    @classmethod
    def from_dict(cls, raw: dict, source_file: Path) -> "FoodProfile":
        code = raw.get("code", source_file.stem)

        fields_present = set(raw.keys())
        attributes_present: set[str] = set()
        if isinstance(raw.get("attributes"), dict):
            attributes_present = set(raw["attributes"].keys())

        foodcategory = None
        np = raw.get("nutritionProps")
        if isinstance(np, dict):
            foodcategory = np.get("foodcategory")
        elif isinstance(raw.get("nutritionPropsByType"), dict):
            for v in raw["nutritionPropsByType"].values():
                if isinstance(v, dict) and "foodcategory" in v:
                    foodcategory = v["foodcategory"]
                    break

        # If no nutrition declared at all, treat as NoNutrition
        if foodcategory is None and "nutritionProps" not in raw and "nutritionPropsByType" not in raw:
            foodcategory = "NoNutrition"

        variant_states: list[str] = []
        vgs = raw.get("variantgroups")
        if isinstance(vgs, list):
            for vg in vgs:
                if isinstance(vg, dict):
                    states = vg.get("states", [])
                    if isinstance(states, list):
                        variant_states.extend(str(s) for s in states)

        food_tags: list[str] = []
        if isinstance(raw.get("attributes"), dict):
            tags = raw["attributes"].get("foodTags", [])
            if isinstance(tags, list):
                food_tags = list(tags)

        return cls(
            code=code,
            source_file=source_file,
            foodcategory=foodcategory,
            variants=[],
            variant_states=variant_states,
            fields_present=fields_present,
            attributes_present=attributes_present,
            food_tags=food_tags,
            raw=raw,
        )

    def has_field(self, dotted_path: str) -> bool:
        """Check a dotted path against the raw dict."""
        cur: Any = self.raw
        for part in dotted_path.split("."):
            if not isinstance(cur, dict):
                return False
            if part not in cur:
                return False
            cur = cur[part]
        return True

    def has_attribute(self, name: str) -> bool:
        """Check attributes.<name>."""
        return self.has_field(f"attributes.{name}")

    def source_file_key(self) -> str:
        """Return 'seafarer:itemtypes/food/chili.json' for patch target lookup."""
        parts = self.source_file.parts
        if "assets" in parts:
            idx = parts.index("assets")
            rest = parts[idx + 1:]
            if len(rest) >= 2:
                domain = rest[0]
                inner = "/".join(rest[1:])
                return f"{domain}:{inner}"
        return str(self.source_file)


@dataclass
class ValidationContext:
    assets_root: Path
    seafarer: Path
    game: Path
    base_game_assets: Path | None
    ef_assets: Path | None

    seafarer_shapes: set[str] = field(default_factory=set)
    seafarer_textures: set[str] = field(default_factory=set)

    base_game_food: dict[str, FoodProfile] = field(default_factory=dict)
    ef_food: dict[str, FoodProfile] = field(default_factory=dict)
    mod_food: dict[str, FoodProfile] = field(default_factory=dict)

    patches_by_target: dict[str, list[PatchRef]] = field(default_factory=dict)
    recipe_usage: dict[str, set[str]] = field(default_factory=dict)
    lang_keys: dict[str, str] = field(default_factory=dict)

    schema_baseline: dict[str, dict[str, float]] = field(default_factory=dict)
    schema_overrides: dict[str, dict[str, list]] = field(default_factory=dict)


def _scan_food_dir(food_dir: Path) -> dict[str, FoodProfile]:
    out: dict[str, FoodProfile] = {}
    if not food_dir.exists():
        return out
    for f in sorted(food_dir.rglob("*.json")):
        data, error = load_json5(f)
        if error or not isinstance(data, dict):
            continue
        profile = FoodProfile.from_dict(data, source_file=f)
        out[profile.code] = profile
    return out


def build_context(
    assets_root: Path,
    base_game_assets: Path | None,
    ef_assets: Path | None,
    load_food: bool = False,
) -> ValidationContext:
    seafarer = assets_root / "seafarer"
    game = assets_root / "game"

    ctx = ValidationContext(
        assets_root=assets_root,
        seafarer=seafarer,
        game=game,
        base_game_assets=base_game_assets,
        ef_assets=ef_assets,
    )

    ctx.seafarer_shapes = build_file_index(seafarer, "shapes")
    ctx.seafarer_textures = build_file_index(seafarer, "textures")

    lang_file = seafarer / "lang" / "en.json"
    if lang_file.exists():
        data, error = load_lang_raw(lang_file)
        if data:
            ctx.lang_keys = data

    if load_food:
        ctx.mod_food = _scan_food_dir(seafarer / "itemtypes" / "food")
        if base_game_assets and base_game_assets.exists():
            ctx.base_game_food = _scan_food_dir(
                base_game_assets / "survival" / "itemtypes" / "food")
        if ef_assets and ef_assets.exists():
            ctx.ef_food = _scan_food_dir(
                ef_assets / "expandedfoods" / "itemtypes" / "food")

        all_patches: list[PatchRef] = []
        all_patches.extend(load_patches(seafarer / "patches"))
        all_patches.extend(load_patches(game / "patches"))
        if ef_assets and ef_assets.exists():
            all_patches.extend(load_patches(ef_assets / "expandedfoods" / "patches"))
            all_patches.extend(load_patches(ef_assets / "game" / "patches"))
        ctx.patches_by_target = patches_by_target(all_patches)
        ctx.recipe_usage = scan_recipe_usage(ctx)

    return ctx


# ── Recipe usage scanner ───────────────────────────────────────────────────────

_RECIPE_FILE_TAGS = {
    "meatystew":  "stew",
    "veggiestew": "stew",
    "stew":       "stew",
    "soup":       "soup",
    "porridge":   "porridge",
    "brined":     "brine",
    "fermented":  "fermenting",
    "candied":    "candying",
    "cured":      "curing",
    "brewing":    "brewing",
    "brew":       "brewing",
    "simmering":  "simmering",
    "mixing":     "mixing",
    "kneading":   "kneading",
}


def _extract_codes_from_recipe(recipe: dict) -> set[str]:
    """Walk a recipe object and collect ingredient 'code' values (stripped of domain)."""
    codes: set[str] = set()

    def walk(node):
        if isinstance(node, dict):
            c = node.get("code")
            if isinstance(c, str):
                bare = c.split(":", 1)[-1]
                if bare and "*" not in bare:
                    codes.add(bare)
            for v in node.values():
                walk(v)
        elif isinstance(node, list):
            for item in node:
                walk(item)

    walk(recipe)
    return codes


def scan_recipe_usage(ctx: ValidationContext) -> dict[str, set[str]]:
    """Build { item_code: {recipe_context_tags} } by scanning recipes."""
    usage: dict[str, set[str]] = {}
    recipe_roots: list[Path] = []

    if (ctx.seafarer / "recipes").exists():
        recipe_roots.append(ctx.seafarer / "recipes")
    if ctx.base_game_assets and (ctx.base_game_assets / "survival" / "recipes").exists():
        recipe_roots.append(ctx.base_game_assets / "survival" / "recipes")
    if ctx.base_game_assets and (ctx.base_game_assets / "game" / "recipes").exists():
        recipe_roots.append(ctx.base_game_assets / "game" / "recipes")

    for root in recipe_roots:
        for f in root.rglob("*.json"):
            data, error = load_json5(f)
            if error or data is None:
                continue
            path_lower = str(f).lower()
            tags: set[str] = set()
            for needle, tag in _RECIPE_FILE_TAGS.items():
                if needle in path_lower:
                    tags.add(tag)
            if not tags:
                continue
            entries = data if isinstance(data, list) else [data]
            for entry in entries:
                if not isinstance(entry, dict):
                    continue
                for code in _extract_codes_from_recipe(entry):
                    usage.setdefault(code, set()).update(tags)

    return usage
