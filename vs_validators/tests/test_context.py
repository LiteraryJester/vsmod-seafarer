import json
from pathlib import Path
import pytest

from vs_validators.context import FoodProfile, ValidationContext, build_context


def test_foodprofile_from_dict_basic():
    raw = {
        "code": "chili",
        "maxstacksize": 64,
        "nutritionProps": {"satiety": 40, "foodcategory": "Vegetable"},
        "attributes": {"foodTags": ["chili"], "inPieProperties": {"texture": "x"}},
        "shape": {"base": "seafarer:item/food/chili"},
    }
    p = FoodProfile.from_dict(raw, source_file=Path("/tmp/chili.json"))
    assert p.code == "chili"
    assert p.foodcategory == "Vegetable"
    assert "nutritionProps" in p.fields_present
    assert "inPieProperties" in p.attributes_present
    assert p.food_tags == ["chili"]


def test_foodprofile_has_field_dotted():
    raw = {
        "code": "chili",
        "nutritionProps": {"satiety": 40, "foodcategory": "Vegetable"},
        "attributes": {"inPieProperties": {"texture": "x"}},
    }
    p = FoodProfile.from_dict(raw, source_file=Path("/tmp/chili.json"))
    assert p.has_field("nutritionProps") is True
    assert p.has_field("nutritionProps.satiety") is True
    assert p.has_field("nutritionProps.missing") is False
    assert p.has_field("attributes.inPieProperties") is True
    assert p.has_field("attributes.inBurritoProperties") is False


def test_foodprofile_variant_states_from_variantgroups():
    raw = {
        "code": "slackedmeat",
        "variantgroups": [
            {"code": "type", "states": ["redmeat", "bushmeat"]},
            {"code": "state", "states": ["whole", "cut", "smashed"]},
        ],
    }
    p = FoodProfile.from_dict(raw, source_file=Path("/tmp/slackedmeat.json"))
    assert set(p.variant_states) == {"redmeat", "bushmeat", "whole", "cut", "smashed"}


def test_foodprofile_source_file_key():
    p = FoodProfile.from_dict(
        {"code": "chili"},
        source_file=Path("/anything/SaltAndSand/assets/seafarer/itemtypes/food/chili.json"),
    )
    key = p.source_file_key()
    assert key == "seafarer:itemtypes/food/chili.json"


def test_build_context_loads_mod_food(tmp_mod):
    food_dir = tmp_mod / "assets" / "seafarer" / "itemtypes" / "food"
    (food_dir / "chili.json").write_text(json.dumps({
        "code": "chili",
        "nutritionProps": {"satiety": 40, "foodcategory": "Vegetable"},
    }))
    (food_dir / "corn.json").write_text(json.dumps({
        "code": "corn",
        "nutritionProps": {"satiety": 60, "foodcategory": "Grain"},
    }))
    (tmp_mod / "assets" / "seafarer" / "lang" / "en.json").write_text('{"item-chili": "Chili"}')

    ctx = build_context(
        assets_root=tmp_mod / "assets",
        base_game_assets=None,
        ef_assets=None,
        load_food=True,
    )
    assert "chili" in ctx.mod_food
    assert ctx.mod_food["chili"].foodcategory == "Vegetable"
    assert "item-chili" in ctx.lang_keys


def test_build_context_skips_food_when_not_requested(tmp_mod):
    ctx = build_context(
        assets_root=tmp_mod / "assets",
        base_game_assets=None,
        ef_assets=None,
        load_food=False,
    )
    assert ctx.mod_food == {}
    assert ctx.base_game_food == {}
