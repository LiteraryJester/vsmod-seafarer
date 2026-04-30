import json
from pathlib import Path
from argparse import Namespace

from vs_validators.common import ValidationResult
from vs_validators.context import build_context, scan_recipe_usage
from vs_validators.food import validate_food


def test_scan_recipe_usage_tags_stew(tmp_mod):
    recipes = tmp_mod / "assets" / "seafarer" / "recipes" / "cooking"
    (recipes / "meatystew.json").write_text(json.dumps({
        "code": "meatystew",
        "ingredients": [
            {"code": "seafarer:chili"},
            {"code": "seafarer:corn"},
        ],
    }))
    ctx = build_context(
        assets_root=tmp_mod / "assets",
        base_game_assets=None,
        ef_assets=None,
        load_food=True,
    )
    usage = scan_recipe_usage(ctx)
    assert "stew" in usage.get("chili", set())
    assert "stew" in usage.get("corn", set())


def test_validate_food_runs_all_rules_on_empty_food(tmp_mod):
    (tmp_mod / "assets" / "seafarer" / "lang" / "en.json").write_text("{}")
    ctx = build_context(
        assets_root=tmp_mod / "assets",
        base_game_assets=None,
        ef_assets=None,
        load_food=True,
    )
    result = ValidationResult()
    args = Namespace(verbose=False, rule="", skip_rule="", file="", dump_baseline=False)
    validate_food(result, ctx, args)
    assert result.errors == []
    assert result.warnings == []


def test_validate_food_reports_issues_on_minimal_vegetable(tmp_mod):
    food_dir = tmp_mod / "assets" / "seafarer" / "itemtypes" / "food"
    (food_dir / "chili.json").write_text(json.dumps({
        "code": "chili",
        "nutritionProps": {"foodcategory": "Vegetable", "satiety": 40},
        "attributes": {"foodTags": ["chili"]},
    }))
    (tmp_mod / "assets" / "seafarer" / "lang" / "en.json").write_text("{}")
    ctx = build_context(
        assets_root=tmp_mod / "assets",
        base_game_assets=None,
        ef_assets=None,
        load_food=True,
    )
    result = ValidationResult()
    args = Namespace(verbose=False, rule="", skip_rule="", file="", dump_baseline=False)
    validate_food(result, ctx, args)
    assert any("inPieProperties" in m or "inBurritoProperties" in m
               for _, m in result.warnings)
