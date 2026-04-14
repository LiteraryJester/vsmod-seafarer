"""Shared pytest fixtures for vs_validators tests."""
from pathlib import Path
import pytest


@pytest.fixture
def tmp_mod(tmp_path):
    """Create a minimal mod directory structure under tmp_path."""
    root = tmp_path / "mod"
    (root / "assets" / "seafarer" / "itemtypes" / "food").mkdir(parents=True)
    (root / "assets" / "seafarer" / "patches").mkdir(parents=True)
    (root / "assets" / "seafarer" / "lang").mkdir(parents=True)
    (root / "assets" / "seafarer" / "recipes" / "cooking").mkdir(parents=True)
    (root / "assets" / "seafarer" / "recipes" / "barrel").mkdir(parents=True)
    (root / "assets" / "seafarer" / "recipes" / "grid").mkdir(parents=True)
    (root / "assets" / "seafarer" / "shapes").mkdir(parents=True)
    (root / "assets" / "seafarer" / "textures").mkdir(parents=True)
    (root / "assets" / "game" / "patches").mkdir(parents=True)
    return root
