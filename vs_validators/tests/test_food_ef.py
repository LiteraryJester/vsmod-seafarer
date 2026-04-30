from pathlib import Path

from vs_validators.common import ValidationResult
from vs_validators.context import FoodProfile, ValidationContext
from vs_validators.indexes import PatchRef
from vs_validators.food.ef import (
    check_ef_protein_variants, check_ef_recipe_coverage,
    EF_PROTEIN_STATES, EF_PROTEIN_REQUIRED_PATCH_PATHS,
)


def _mk(code, category, raw_extras=None):
    raw = {"code": code, "nutritionProps": {"foodcategory": category, "satiety": 40}}
    if raw_extras:
        raw.update(raw_extras)
    return FoodProfile.from_dict(
        raw,
        source_file=Path(f"/anything/assets/seafarer/itemtypes/food/{code}.json"),
    )


def _ctx(patches_by_target=None, ef_assets=Path("/tmp/ef")):
    ctx = ValidationContext(
        assets_root=Path("/tmp"),
        seafarer=Path("/tmp/seafarer"),
        game=Path("/tmp/game"),
        base_game_assets=None,
        ef_assets=ef_assets,
    )
    ctx.patches_by_target = patches_by_target or {}
    return ctx


def _full_patchset(file_target):
    """Build a PatchRef set that matches every required EF protein path."""
    patches = [
        PatchRef(source_file=Path("/tmp/ef-meats.json"), op="replace",
                 path="/variantgroups/1/states", file=file_target,
                 value=list(EF_PROTEIN_STATES),
                 depends_on=[{"modid": "expandedfoods"}]),
    ]
    for path in [
        "/attributes/bakingPropertiesByType",
        "/nutritionPropsByType",
        "/attributes/nutritionPropsWhenInMealByType",
        "/attributes/inPiePropertiesByType",
        "/transitionablePropsByType",
        "/combustiblePropsByType",
    ]:
        patches.append(PatchRef(
            source_file=Path("/tmp/ef-meats.json"), op="addmerge",
            path=path, file=file_target, value={},
            depends_on=[{"modid": "expandedfoods"}],
        ))
    return patches


def test_ef_protein_silent_for_non_protein():
    profile = _mk("chili", "Vegetable")
    ctx = _ctx()
    result = ValidationResult()
    check_ef_protein_variants(profile, ctx, result)
    assert result.errors == []


def test_ef_protein_skipped_when_no_ef_assets():
    profile = _mk("slackedmeat", "Protein")
    ctx = _ctx(ef_assets=None)
    result = ValidationResult()
    check_ef_protein_variants(profile, ctx, result)
    assert result.errors == []


def test_ef_protein_errors_with_no_patches():
    profile = _mk("slackedmeat", "Protein")
    ctx = _ctx(patches_by_target={"seafarer:itemtypes/food/slackedmeat.json": []})
    result = ValidationResult()
    check_ef_protein_variants(profile, ctx, result)
    assert any("no EF" in msg for _, msg in result.errors)


def test_ef_protein_errors_when_missing_a_state():
    profile = _mk("slackedmeat", "Protein")
    target = "seafarer:itemtypes/food/slackedmeat.json"
    patches = _full_patchset(target)
    states = [s for s in patches[0].value if s != "smashed"]
    patches[0] = PatchRef(
        source_file=patches[0].source_file, op=patches[0].op, path=patches[0].path,
        file=patches[0].file, value=states, depends_on=patches[0].depends_on,
    )
    ctx = _ctx(patches_by_target={target: patches})
    result = ValidationResult()
    check_ef_protein_variants(profile, ctx, result)
    assert any("smashed" in msg for _, msg in result.errors)


def test_ef_protein_errors_when_missing_required_patch_path():
    profile = _mk("slackedmeat", "Protein")
    target = "seafarer:itemtypes/food/slackedmeat.json"
    patches = _full_patchset(target)
    patches = [p for p in patches if p.path != "/combustiblePropsByType"]
    ctx = _ctx(patches_by_target={target: patches})
    result = ValidationResult()
    check_ef_protein_variants(profile, ctx, result)
    assert any("combustiblePropsByType" in msg for _, msg in result.errors)


def test_ef_protein_passes_with_full_patchset():
    profile = _mk("slackedmeat", "Protein")
    target = "seafarer:itemtypes/food/slackedmeat.json"
    ctx = _ctx(patches_by_target={target: _full_patchset(target)})
    result = ValidationResult()
    check_ef_protein_variants(profile, ctx, result)
    assert result.errors == []


def test_ef_coverage_warns_when_not_referenced(tmp_path):
    ef_patches_dir = tmp_path / "expandedfoods" / "patches"
    ef_patches_dir.mkdir(parents=True)
    (ef_patches_dir / "unrelated.json").write_text(
        '[{"op": "add", "path": "/a", "file": "game:x.json", "value": {"code": "other"}}]'
    )
    profile = _mk("newfood", "Vegetable")
    ctx = _ctx(ef_assets=tmp_path)
    result = ValidationResult()
    check_ef_recipe_coverage(profile, ctx, result)
    assert any("newfood" in msg for _, msg in result.warnings)


def test_ef_coverage_passes_when_referenced(tmp_path):
    ef_patches_dir = tmp_path / "expandedfoods" / "patches"
    ef_patches_dir.mkdir(parents=True)
    (ef_patches_dir / "ingredients.json").write_text(
        '[{"op": "add", "path": "/-", "file": "expandedfoods:recipes/x.json",'
        ' "value": {"ingredients": {"C": {"code": "seafarer:chili"}}}}]'
    )
    profile = _mk("chili", "Vegetable")
    ctx = _ctx(ef_assets=tmp_path)
    result = ValidationResult()
    check_ef_recipe_coverage(profile, ctx, result)
    assert result.warnings == []
