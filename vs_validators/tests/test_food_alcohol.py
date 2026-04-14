import json
from pathlib import Path

from vs_validators.common import ValidationResult
from vs_validators.context import FoodProfile, ValidationContext
from vs_validators.food.alcohol import check_brewing_coverage


def _mk(code, category):
    return FoodProfile.from_dict(
        {"code": code, "nutritionProps": {"foodcategory": category, "satiety": 40}},
        source_file=Path(f"/anything/assets/seafarer/itemtypes/food/{code}.json"),
    )


def _ctx_with_patches_dir(tmp_path, brewing_json_content=None):
    seafarer = tmp_path / "seafarer"
    patches = seafarer / "patches"
    patches.mkdir(parents=True)
    if brewing_json_content is not None:
        (patches / "brewing.json").write_text(brewing_json_content)
    return ValidationContext(
        assets_root=tmp_path,
        seafarer=seafarer,
        game=tmp_path / "game",
        base_game_assets=None,
        ef_assets=None,
    )


def test_alcohol_silent_for_protein(tmp_path):
    profile = _mk("slackedmeat", "Protein")
    ctx = _ctx_with_patches_dir(tmp_path)
    result = ValidationResult()
    check_brewing_coverage(profile, ctx, result)
    assert result.warnings == []


def test_alcohol_warns_when_fruit_has_no_brewing(tmp_path):
    profile = _mk("coconut", "Fruit")
    ctx = _ctx_with_patches_dir(tmp_path)
    result = ValidationResult()
    check_brewing_coverage(profile, ctx, result)
    assert any("brewing" in msg for _, msg in result.warnings)


def test_alcohol_passes_when_fruit_in_brewing_json(tmp_path):
    profile = _mk("coconut", "Fruit")
    brewing = json.dumps([{
        "op": "add", "path": "/-", "file": "game:recipes/barrel/brewing.json",
        "value": {"ingredients": [{"code": "seafarer:coconut"}]},
    }])
    ctx = _ctx_with_patches_dir(tmp_path, brewing_json_content=brewing)
    result = ValidationResult()
    check_brewing_coverage(profile, ctx, result)
    assert result.warnings == []


def test_alcohol_warns_for_grain_with_no_coverage(tmp_path):
    profile = _mk("corn", "Grain")
    ctx = _ctx_with_patches_dir(tmp_path)
    result = ValidationResult()
    check_brewing_coverage(profile, ctx, result)
    assert any("brewing" in msg.lower() for _, msg in result.warnings)
