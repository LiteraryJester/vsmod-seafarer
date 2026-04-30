from pathlib import Path

from vs_validators.common import ValidationResult
from vs_validators.context import FoodProfile
from vs_validators.food.schema import (
    load_overrides, merge_tiers, check_food_schema,
)


def _mk(code, category, extras=None):
    raw = {"code": code, "nutritionProps": {"foodcategory": category, "satiety": 40}}
    if extras:
        raw.update(extras)
    return FoodProfile.from_dict(raw, source_file=Path(f"/tmp/{code}.json"))


def test_load_overrides_reads_yaml():
    overrides = load_overrides()
    assert "Vegetable" in overrides
    assert "nutritionProps.satiety" in overrides["Vegetable"]["require"]


def test_merge_tiers_override_wins_over_baseline():
    baseline = {"nutritionProps.satiety": 0.4}
    override = {"require": ["nutritionProps.satiety"], "recommend": [], "ignore": []}
    effective = merge_tiers(baseline, override)
    assert effective["nutritionProps.satiety"] == "required"


def test_merge_tiers_ignore_removes_field():
    baseline = {"fpHandTransform": 1.0}
    override = {"require": [], "recommend": [], "ignore": ["fpHandTransform"]}
    effective = merge_tiers(baseline, override)
    assert "fpHandTransform" not in effective


def test_merge_tiers_falls_through_to_statistical():
    baseline = {"shape": 1.0, "guiTransform": 0.7}
    override = {"require": [], "recommend": [], "ignore": []}
    effective = merge_tiers(baseline, override)
    assert effective["shape"] == "required"
    assert effective["guiTransform"] == "expected"


def test_check_food_schema_errors_on_missing_required():
    profile = _mk("chili", "Vegetable")
    profile.raw.pop("attributes", None)
    profile.attributes_present = set()

    baseline = {"Vegetable": {"code": 1.0, "nutritionProps.satiety": 1.0}}
    overrides = {"Vegetable": {"require": ["attributes.foodTags"], "recommend": [], "ignore": []}}
    result = ValidationResult()
    check_food_schema(profile, baseline, overrides, result)
    assert any("attributes.foodTags" in msg for _, msg in result.errors)


def test_check_food_schema_warns_on_missing_recommended():
    profile = _mk("chili", "Vegetable", {"attributes": {"foodTags": ["chili"]}})
    baseline = {"Vegetable": {"code": 1.0}}
    overrides = {"Vegetable": {
        "require": [],
        "recommend": ["attributes.inPieProperties"],
        "ignore": [],
    }}
    result = ValidationResult()
    check_food_schema(profile, baseline, overrides, result)
    assert any("attributes.inPieProperties" in msg for _, msg in result.warnings)


def test_check_food_schema_passes_when_all_present():
    profile = _mk("chili", "Vegetable", {
        "attributes": {"foodTags": ["chili"], "inPieProperties": {}},
    })
    baseline = {"Vegetable": {}}
    overrides = {"Vegetable": {
        "require": ["attributes.foodTags"],
        "recommend": ["attributes.inPieProperties"],
        "ignore": [],
    }}
    result = ValidationResult()
    check_food_schema(profile, baseline, overrides, result)
    assert result.errors == []
    assert result.warnings == []
