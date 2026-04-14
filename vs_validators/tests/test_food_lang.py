from pathlib import Path

from vs_validators.common import ValidationResult
from vs_validators.context import FoodProfile, ValidationContext
from vs_validators.food.lang import (
    check_recipe_driven_lang, check_category_driven_lang, RECIPE_LANG_MAP,
)


def _mk(code, category, extras=None):
    raw = {"code": code, "nutritionProps": {"foodcategory": category, "satiety": 40}}
    if extras:
        raw.update(extras)
    return FoodProfile.from_dict(
        raw,
        source_file=Path(f"/anything/assets/seafarer/itemtypes/food/{code}.json"),
    )


def _ctx(recipe_usage=None, lang_keys=None):
    ctx = ValidationContext(
        assets_root=Path("/tmp"),
        seafarer=Path("/tmp/seafarer"),
        game=Path("/tmp/game"),
        base_game_assets=None,
        ef_assets=None,
    )
    ctx.recipe_usage = recipe_usage or {}
    ctx.lang_keys = lang_keys or {}
    return ctx


def test_recipe_driven_errors_when_key_missing():
    profile = _mk("chili", "Vegetable")
    ctx = _ctx(recipe_usage={"chili": {"stew"}}, lang_keys={})
    result = ValidationResult()
    check_recipe_driven_lang(profile, ctx, result)
    assert any("recipeingredient-instew-chili" in msg for _, msg in result.errors)


def test_recipe_driven_passes_when_key_present():
    profile = _mk("chili", "Vegetable")
    ctx = _ctx(
        recipe_usage={"chili": {"stew"}},
        lang_keys={"recipeingredient-instew-chili": "Chili"},
    )
    result = ValidationResult()
    check_recipe_driven_lang(profile, ctx, result)
    assert result.errors == []


def test_recipe_driven_silent_when_item_not_in_any_recipe():
    profile = _mk("chili", "Vegetable")
    ctx = _ctx(recipe_usage={}, lang_keys={})
    result = ValidationResult()
    check_recipe_driven_lang(profile, ctx, result)
    assert result.errors == []


def test_category_driven_warns_on_suggested_missing():
    profile = _mk("chili", "Vegetable")
    ctx = _ctx(lang_keys={})
    result = ValidationResult()
    check_category_driven_lang(profile, ctx, result)
    assert any("recipeingredient-instew-chili" in msg for _, msg in result.warnings)
    assert any("recipeingredient-inbrine-chili" in msg for _, msg in result.warnings)


def test_category_driven_respects_opt_out():
    profile = _mk("chili", "Vegetable", {
        "attributes": {"validationSkip": ["inbrine", "infermented"]},
    })
    ctx = _ctx(lang_keys={})
    result = ValidationResult()
    check_category_driven_lang(profile, ctx, result)
    msgs = [m for _, m in result.warnings]
    assert not any("inbrine" in m for m in msgs)
    assert not any("infermented" in m for m in msgs)
    assert any("instew" in m for m in msgs)


def test_recipe_driven_ignores_opt_out():
    profile = _mk("chili", "Vegetable", {
        "attributes": {"validationSkip": ["instew"]},
    })
    ctx = _ctx(recipe_usage={"chili": {"stew"}}, lang_keys={})
    result = ValidationResult()
    check_recipe_driven_lang(profile, ctx, result)
    assert any("recipeingredient-instew-chili" in msg for _, msg in result.errors)
