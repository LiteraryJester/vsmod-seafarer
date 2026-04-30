from pathlib import Path
import pytest

from vs_validators.common import ValidationResult, load_json5, rel


def test_validation_result_starts_empty():
    r = ValidationResult()
    assert r.passed == 0
    assert r.errors == []
    assert r.warnings == []
    assert r.total == 0


def test_validation_result_records_error():
    r = ValidationResult()
    r.error(Path("/tmp/foo.json"), "bad")
    assert len(r.errors) == 1
    assert r.errors[0] == (Path("/tmp/foo.json"), "bad")


def test_validation_result_records_warning_and_ok():
    r = ValidationResult()
    r.warning(Path("/tmp/foo.json"), "maybe bad")
    r.ok()
    r.ok()
    assert len(r.warnings) == 1
    assert r.passed == 2
    assert r.total == 2


def test_load_json5_parses_comments_and_trailing_commas(tmp_path):
    f = tmp_path / "test.json"
    f.write_text('// comment\n{"a": 1, "b": [1, 2,],}', encoding="utf-8")
    data, error = load_json5(f)
    assert error is None
    assert data == {"a": 1, "b": [1, 2]}


def test_load_json5_returns_error_on_garbage(tmp_path):
    f = tmp_path / "bad.json"
    f.write_text("not json at all {{{", encoding="utf-8")
    data, error = load_json5(f)
    assert data is None
    assert error is not None


def test_rel_shortens_path_under_assets_root(tmp_path):
    assets_root = tmp_path / "assets"
    assets_root.mkdir()
    path = assets_root / "seafarer" / "itemtypes" / "chili.json"
    assert rel(path, assets_root) == "seafarer/itemtypes/chili.json"


def test_validation_result_errors_and_findings_stay_in_sync():
    r = ValidationResult()
    r.error(Path("/tmp/a.json"), "bad thing", rule_id="food.pie")
    r.warning(Path("/tmp/b.json"), "maybe bad", rule_id="food.burrito")

    # Tuple-list API (used by existing asset validators)
    assert r.errors == [(Path("/tmp/a.json"), "bad thing")]
    assert r.warnings == [(Path("/tmp/b.json"), "maybe bad")]

    # Rich findings API (used by future --json output)
    assert len(r.findings) == 2
    assert r.findings[0].rule_id == "food.pie"
    assert r.findings[0].severity == "error"
    assert r.findings[1].rule_id == "food.burrito"
    assert r.findings[1].severity == "warning"
