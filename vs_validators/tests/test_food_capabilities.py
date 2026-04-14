from pathlib import Path

from vs_validators.common import ValidationResult
from vs_validators.context import FoodProfile, ValidationContext
from vs_validators.indexes import PatchRef
from vs_validators.food.capabilities import check_pie_support, check_burrito_support


def _mk(code, category, extras=None):
    raw = {"code": code, "nutritionProps": {"foodcategory": category, "satiety": 40}}
    if extras:
        raw.update(extras)
    return FoodProfile.from_dict(
        raw,
        source_file=Path(f"/anything/assets/seafarer/itemtypes/food/{code}.json"),
    )


def _ctx_with_patches(target_key, patches):
    ctx = ValidationContext(
        assets_root=Path("/tmp"),
        seafarer=Path("/tmp/seafarer"),
        game=Path("/tmp/game"),
        base_game_assets=None,
        ef_assets=None,
    )
    ctx.patches_by_target = {target_key: patches}
    return ctx


def test_pie_silent_for_grain():
    profile = _mk("corn", "Grain")
    ctx = _ctx_with_patches("seafarer:itemtypes/food/corn.json", [])
    result = ValidationResult()
    check_pie_support(profile, ctx, result)
    assert result.warnings == []


def test_pie_warns_when_vegetable_lacks_attribute():
    profile = _mk("chili", "Vegetable")
    ctx = _ctx_with_patches("seafarer:itemtypes/food/chili.json", [])
    result = ValidationResult()
    check_pie_support(profile, ctx, result)
    assert any("inPieProperties" in msg for _, msg in result.warnings)


def test_pie_passes_when_attribute_present():
    profile = _mk("chili", "Vegetable", {"attributes": {"inPieProperties": {"texture": "x"}}})
    ctx = _ctx_with_patches("seafarer:itemtypes/food/chili.json", [])
    result = ValidationResult()
    check_pie_support(profile, ctx, result)
    assert result.warnings == []


def test_pie_passes_when_by_type_variant_present():
    profile = _mk("slackedmeat", "Protein", {"attributes": {"inPiePropertiesByType": {}}})
    ctx = _ctx_with_patches("seafarer:itemtypes/food/slackedmeat.json", [])
    result = ValidationResult()
    check_pie_support(profile, ctx, result)
    assert result.warnings == []


def test_pie_passes_when_patch_adds_it():
    profile = _mk("slackedmeat", "Protein")
    patch = PatchRef(
        source_file=Path("/tmp/ef.json"),
        op="addmerge",
        path="/attributes/inPiePropertiesByType",
        file="seafarer:itemtypes/food/slackedmeat.json",
        value={},
        depends_on=[{"modid": "expandedfoods"}],
    )
    ctx = _ctx_with_patches("seafarer:itemtypes/food/slackedmeat.json", [patch])
    result = ValidationResult()
    check_pie_support(profile, ctx, result)
    assert result.warnings == []


def test_burrito_warns_when_vegetable_lacks_attribute():
    profile = _mk("chili", "Vegetable")
    ctx = _ctx_with_patches("seafarer:itemtypes/food/chili.json", [])
    result = ValidationResult()
    check_burrito_support(profile, ctx, result)
    assert any("inBurritoProperties" in msg for _, msg in result.warnings)
