import json
from pathlib import Path
import pytest

from vs_validators.indexes import (
    PatchRef, path_matches, build_file_index, load_patches, load_lang_raw
)


def test_path_matches_exact():
    assert path_matches("/attributes/foo", "/attributes/foo") is True


def test_path_matches_wildcard_segment():
    assert path_matches("/variantgroups/0/states", "/variantgroups/*/states") is True
    assert path_matches("/variantgroups/1/states", "/variantgroups/*/states") is True
    assert path_matches("/variantgroups/0/code", "/variantgroups/*/states") is False


def test_path_matches_prefix_wildcard():
    assert path_matches("/shapeByType/@*-(smashed)", "/shapeByType/*") is True
    assert path_matches("/attributes/inPieProperties", "/shapeByType/*") is False


def test_patchref_depends_on_mod():
    p = PatchRef(
        source_file=Path("/tmp/x.json"),
        op="add",
        path="/attributes/foo",
        file="seafarer:itemtypes/food/x.json",
        value={"a": 1},
        depends_on=[{"modid": "expandedfoods"}],
    )
    assert p.depends_on_mod("expandedfoods") is True
    assert p.depends_on_mod("butchering") is False


def test_build_file_index_strips_extension(tmp_path):
    base = tmp_path / "shapes"
    (base / "item" / "food").mkdir(parents=True)
    (base / "item" / "food" / "chili.json").write_text("{}")
    (base / "item" / "food" / "corn.json").write_text("{}")
    index = build_file_index(tmp_path, "shapes")
    assert "item/food/chili" in index
    assert "item/food/corn" in index


def test_load_patches_parses_array(tmp_path):
    patch_dir = tmp_path / "patches"
    patch_dir.mkdir()
    data = [
        {
            "op": "add",
            "path": "/attributes/foo",
            "file": "seafarer:itemtypes/food/chili.json",
            "value": {"a": 1},
        },
        {
            "op": "replace",
            "path": "/maxstacksize",
            "file": "seafarer:itemtypes/food/chili.json",
            "value": 32,
            "dependsOn": [{"modid": "expandedfoods"}],
        },
    ]
    (patch_dir / "chili.json").write_text(json.dumps(data))
    patches = load_patches(patch_dir)
    assert len(patches) == 2
    assert patches[0].op == "add"
    assert patches[1].depends_on_mod("expandedfoods") is True


def test_load_patches_parses_single_object(tmp_path):
    patch_dir = tmp_path / "patches"
    patch_dir.mkdir()
    (patch_dir / "one.json").write_text(
        '{"op": "add", "path": "/a", "file": "game:foo.json", "value": 1}'
    )
    patches = load_patches(patch_dir)
    assert len(patches) == 1
    assert patches[0].op == "add"


def test_load_lang_raw_handles_embedded_newlines(tmp_path):
    lang_file = tmp_path / "en.json"
    lang_file.write_text('{\n  "greeting": "hello\nworld",\n  "name": "bob"\n}')
    data, err = load_lang_raw(lang_file)
    assert err is None
    assert data["greeting"] == "hello\nworld"
    assert data["name"] == "bob"
