"""Brewing/alcohol coverage check for Fruit and Grain foods."""
from __future__ import annotations

from vs_validators.common import ValidationResult
from vs_validators.context import FoodProfile, ValidationContext


ALCOHOL_CATEGORIES = {"Fruit", "Grain"}


_PROCESSED_FOOD_PREFIXES = {"flatbread", "tortilla", "masa", "nixtamal", "candied", "dried", "toasted", "roasted"}


def check_brewing_coverage(
    profile: FoodProfile, ctx: ValidationContext, result: ValidationResult,
) -> None:
    if profile.foodcategory not in ALCOHOL_CATEGORIES:
        return
    # Skip processed forms — only raw grain/fruit should have brewing
    code_lower = profile.code.lower()
    if any(code_lower.startswith(prefix) for prefix in _PROCESSED_FOOD_PREFIXES):
        return

    brewing_patch = ctx.seafarer / "patches" / "brewing.json"
    if brewing_patch.exists():
        try:
            text = brewing_patch.read_text(encoding="utf-8-sig")
        except OSError:
            text = ""
        if f'"{profile.code}"' in text or f':{profile.code}"' in text:
            return

    if "brewing" in ctx.recipe_usage.get(profile.code, set()):
        return

    result.warning(
        profile.source_file,
        f"[{profile.code}] {profile.foodcategory} food has no brewing/alcohol coverage",
        rule_id="food.alcohol",
    )
