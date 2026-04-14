"""Pie and burrito support checks."""
from __future__ import annotations

from vs_validators.common import ValidationResult
from vs_validators.context import FoodProfile, ValidationContext
from vs_validators.indexes import PatchRef


CATEGORIES_WITH_PIE_BURRITO = {"Vegetable", "Fruit", "Protein", "Dairy"}


def _patch_adds_attribute(patches: list[PatchRef], attr_names: list[str]) -> bool:
    """True if any patch op adds one of the given attribute names under /attributes/*."""
    for p in patches:
        if p.op not in ("add", "addmerge", "replace"):
            continue
        for attr in attr_names:
            if p.path.startswith(f"/attributes/{attr}"):
                return True
            if p.path == "/attributes" and isinstance(p.value, dict) and attr in p.value:
                return True
    return False


def _check_capability(
    profile: FoodProfile,
    ctx: ValidationContext,
    result: ValidationResult,
    *,
    attr_base: str,
    rule_id: str,
) -> None:
    if profile.foodcategory not in CATEGORIES_WITH_PIE_BURRITO:
        return

    by_type = f"{attr_base}ByType"
    if profile.has_attribute(attr_base) or profile.has_attribute(by_type):
        return

    patches = ctx.patches_by_target.get(profile.source_file_key(), [])
    if _patch_adds_attribute(patches, [attr_base, by_type]):
        return

    result.warning(
        profile.source_file,
        f"[{profile.code}] {profile.foodcategory} food has no attributes.{attr_base}",
        rule_id=rule_id,
    )


def check_pie_support(profile: FoodProfile, ctx: ValidationContext, result: ValidationResult) -> None:
    _check_capability(profile, ctx, result, attr_base="inPieProperties", rule_id="food.pie")


def check_burrito_support(profile: FoodProfile, ctx: ValidationContext, result: ValidationResult) -> None:
    _check_capability(profile, ctx, result, attr_base="inBurritoProperties", rule_id="food.burrito")
