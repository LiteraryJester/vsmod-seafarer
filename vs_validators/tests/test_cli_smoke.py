"""Smoke test: run validate-assets.py end-to-end against the real mod."""
import subprocess
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]


def test_cli_blocktypes_runs_without_crash():
    result = subprocess.run(
        [sys.executable, "validate-assets.py", "--type", "blocktypes"],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        timeout=60,
    )
    assert result.returncode in (0, 1), f"validator crashed: {result.stderr}"
    assert "Blocktypes" in result.stdout


def test_cli_help_shows_food_type():
    result = subprocess.run(
        [sys.executable, "validate-assets.py", "--help"],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        timeout=30,
    )
    assert result.returncode == 0
    assert "food" in result.stdout
