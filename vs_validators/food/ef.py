"""ExpandedFoods compatibility checks."""
from __future__ import annotations

from pathlib import Path

from vs_validators.common import ValidationResult
from vs_validators.context import FoodProfile, ValidationContext
from vs_validators.indexes import PatchRef, path_matches


EF_PROTEIN_STATES = {
    "whole", "cut", "smashed", "nugget",
    "tenderpartbaked", "tender", "tendercharred",
    "nuggetpartbaked", "nuggetcooked", "nuggetcharred",
}

EF_PROTEIN_REQUIRED_PATCH_PATHS = [
    "/variantgroups/*/states",
    "/attributes/bakingPropertiesByType",
    "/nutritionPropsByType",
    "/attributes/nutritionPropsWhenInMealByType",
    "/attributes/inPiePropertiesByType",
    "/transitionablePropsByType",
    "/combustiblePropsByType",
]


def _ef_patches_for(profile: FoodProfile, ctx: ValidationContext) -> list[PatchRef]:
    patches = ctx.patches_by_target.get(profile.source_file_key(), [])
    return [p for p in patches if p.depends_on_mod("expandedfoods")]


def _collect_patched_states(patches: list[PatchRef]) -> set[str]:
    states: set[str] = set()
    for p in patches:
        if "states" not in p.path:
            continue
        if isinstance(p.value, list):
            states.update(str(s) for s in p.value)
    return states


_COOKED_PREFIXES = {"seared", "panseared", "dried", "fermented", "roasted", "toasted", "smoked", "cured", "candied", "salted"}


def check_ef_protein_variants(
    profile: FoodProfile, ctx: ValidationContext, result: ValidationResult,
) -> None:
    if profile.foodcategory != "Protein":
        return
    if ctx.ef_assets is None:
        return
    if "ef_protein" in profile.validation_skip():
        return
    # Skip cooked/preserved forms — EF variants are for raw ingredients only
    code_lower = profile.code.lower()
    if any(code_lower.startswith(prefix) for prefix in _COOKED_PREFIXES):
        return

    ef_patches = _ef_patches_for(profile, ctx)
    if not ef_patches:
        result.error(
            profile.source_file,
            f"[{profile.code}] Protein has no EF dependsOn patches",
            rule_id="food.ef_protein",
        )
        return

    patched_paths = [p.path_pattern() for p in ef_patches]
    for required in EF_PROTEIN_REQUIRED_PATCH_PATHS:
        if not any(path_matches(pp, required) for pp in patched_paths):
            result.error(
                profile.source_file,
                f"[{profile.code}] missing required EF patch at {required}",
                rule_id="food.ef_protein",
            )

    added_states = _collect_patched_states(ef_patches)
    missing_states = EF_PROTEIN_STATES - added_states
    if missing_states:
        result.error(
            profile.source_file,
            f"[{profile.code}] EF patch does not add states: {sorted(missing_states)}",
            rule_id="food.ef_protein",
        )


def _code_needles(code: str) -> tuple[str, ...]:
    return (f'"{code}"', f':{code}"', f":{code}-", f":{code}_")


def _scan_ef_patches_for_code(code: str, ef_assets: Path) -> bool:
    """Return True if the item code appears anywhere in EF patch JSON text."""
    if not ef_assets.exists():
        return False
    needles = _code_needles(code)
    for root in (ef_assets / "expandedfoods" / "patches", ef_assets / "game" / "patches"):
        if not root.exists():
            continue
        for f in root.rglob("*.json"):
            try:
                text = f.read_text(encoding="utf-8-sig")
            except OSError:
                continue
            if any(n in text for n in needles):
                return True
    return False


def check_ef_recipe_coverage(
    profile: FoodProfile, ctx: ValidationContext, result: ValidationResult,
) -> None:
    if ctx.ef_assets is None:
        return
    if "ef_coverage" in profile.validation_skip():
        return
    # Skip cooked/preserved forms — EF coverage tracks raw ingredients
    code_lower = profile.code.lower()
    if any(code_lower.startswith(prefix) for prefix in _COOKED_PREFIXES):
        return
    if _scan_ef_patches_for_code(profile.code, ctx.ef_assets):
        return
    mod_ef_dir = ctx.seafarer / "patches"
    if mod_ef_dir.exists():
        needles = _code_needles(profile.code)
        for f in mod_ef_dir.glob("expandedfoods-*.json"):
            try:
                text = f.read_text(encoding="utf-8-sig")
            except OSError:
                continue
            if any(n in text for n in needles):
                return
    result.warning(
        profile.source_file,
        f"[{profile.code}] not referenced in any expandedfoods-*.json patch",
        rule_id="food.ef_coverage",
    )
