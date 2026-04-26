"""Food lang key checks."""
from __future__ import annotations

from vs_validators.common import ValidationResult
from vs_validators.context import FoodProfile, ValidationContext


# Recipe context tag -> lang key template.
# NOTE: These templates are best-effort placeholders. Before using this rule
# against real mod content, verify each key against the base game lang file
# at /mnt/d/Development/vs/assets/game/lang/en.json and update to match the
# actual VS convention. "inbrine" in particular may be a different verb.
RECIPE_LANG_MAP = {
    "stew":       "recipeingredient-instew-{code}",
    "soup":       "recipeingredient-insoup-{code}",
    "porridge":   "recipeingredient-inporridge-{code}",
    "brine":      "recipeingredient-inbrine-{code}",
    "fermenting": "recipeingredient-infermented-{code}",
    "candying":   "recipeingredient-incandied-{code}",
    "curing":     "recipeingredient-incured-{code}",
    "brewing":    "recipeingredient-inalcohol-{code}",
    "simmering":  "recipeingredient-insimmered-{code}",
    "mixing":     "recipeingredient-inmix-{code}",
    "kneading":   "recipeingredient-inknead-{code}",
}


CATEGORY_SUGGESTED_CONTEXTS = {
    "Vegetable": ["instew", "insoup", "inbrine", "infermented"],
    "Fruit":     ["incandied", "inbrine", "inalcohol"],
    "Protein":   ["instew", "insoup", "incured", "insmoked"],
    "Dairy":     ["instew", "insoup"],
    "Grain":     ["inporridge", "inalcohol"],
}


def check_recipe_driven_lang(
    profile: FoodProfile, ctx: ValidationContext, result: ValidationResult,
) -> None:
    contexts = ctx.recipe_usage.get(profile.code, set())
    for ctx_tag in contexts:
        template = RECIPE_LANG_MAP.get(ctx_tag)
        if template is None:
            continue
        expected = template.format(code=profile.code)
        if expected not in ctx.lang_keys:
            result.error(
                profile.source_file,
                f"[{profile.code}] used in {ctx_tag} but missing lang key '{expected}'",
                rule_id="food.lang_recipe",
            )


def check_category_driven_lang(
    profile: FoodProfile, ctx: ValidationContext, result: ValidationResult,
) -> None:
    suggested = CATEGORY_SUGGESTED_CONTEXTS.get(profile.foodcategory, [])
    opt_out = profile.validation_skip()
    for ctx_tag in suggested:
        if ctx_tag in opt_out:
            continue
        expected = f"recipeingredient-{ctx_tag}-{profile.code}"
        if expected not in ctx.lang_keys:
            result.warning(
                profile.source_file,
                f"[{profile.code}] {profile.foodcategory} likely needs lang key '{expected}'",
                rule_id="food.lang_category",
            )
