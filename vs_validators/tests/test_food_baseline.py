from pathlib import Path

from vs_validators.context import FoodProfile
from vs_validators.food.baseline import compute_baseline, tier_for


def _mk(code, category, raw_extras=None):
    raw = {"code": code, "nutritionProps": {"foodcategory": category}}
    if raw_extras:
        raw.update(raw_extras)
    return FoodProfile.from_dict(raw, source_file=Path(f"/tmp/{code}.json"))


def test_compute_baseline_groups_by_category():
    profiles = {
        "a": _mk("a", "Vegetable", {"maxstacksize": 64, "attributes": {"inPieProperties": {}}}),
        "b": _mk("b", "Vegetable", {"maxstacksize": 32, "attributes": {"inPieProperties": {}}}),
        "c": _mk("c", "Vegetable", {"maxstacksize": 16}),
        "d": _mk("d", "Protein", {"transitionableProps": []}),
    }
    baseline = compute_baseline(profiles)
    assert "Vegetable" in baseline
    assert "Protein" in baseline
    assert baseline["Vegetable"]["maxstacksize"] == 1.0
    assert round(baseline["Vegetable"]["attributes.inPieProperties"], 2) == 0.67
    assert baseline["Protein"]["transitionableProps"] == 1.0


def test_tier_for_required():
    assert tier_for(1.0) == "required"
    assert tier_for(0.95) == "required"


def test_tier_for_expected():
    assert tier_for(0.9) == "expected"
    assert tier_for(0.5) == "expected"


def test_tier_for_optional():
    assert tier_for(0.4) == "optional"
    assert tier_for(0.0) == "optional"


def test_compute_baseline_handles_empty():
    assert compute_baseline({}) == {}


def test_compute_baseline_ignores_profiles_without_category():
    # Items with no nutritionProps now get implicit NoNutrition category,
    # so they appear in the baseline under that category.
    profiles = {
        "a": FoodProfile.from_dict({"code": "a"}, source_file=Path("/tmp/a.json")),
    }
    baseline = compute_baseline(profiles)
    assert "NoNutrition" in baseline
    assert baseline["NoNutrition"] == {"code": 1.0}
