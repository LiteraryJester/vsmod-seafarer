"""Food schema check: merge statistical baseline with hand-written overrides."""
from __future__ import annotations

from pathlib import Path
from typing import Mapping

import yaml

from vs_validators.common import ValidationResult
from vs_validators.context import FoodProfile
from vs_validators.food.baseline import tier_for


OVERRIDES_PATH = Path(__file__).parent / "schema_overrides.yaml"


def load_overrides(path: Path | None = None) -> dict[str, dict[str, list[str]]]:
    p = path or OVERRIDES_PATH
    if not p.exists():
        return {}
    with open(p, "r", encoding="utf-8") as f:
        raw = yaml.safe_load(f) or {}
    # Extract _defaults ignore list to merge into every category
    defaults = raw.pop("_defaults", None) or {}
    default_ignore = list((defaults.get("ignore") or []))

    out: dict[str, dict[str, list[str]]] = {}
    for category, rules in raw.items():
        rules = rules or {}
        category_ignore = list(rules.get("ignore", []))
        merged_ignore = list(set(default_ignore + category_ignore))
        out[category] = {
            "require": list(rules.get("require", [])),
            "recommend": list(rules.get("recommend", [])),
            "ignore": merged_ignore,
        }
    return out


def merge_tiers(
    baseline_freq: Mapping[str, float],
    override: Mapping[str, list[str]],
) -> dict[str, str]:
    """Return effective tier per field: 'required', 'expected', 'optional'."""
    required = set(override.get("require", []))
    recommend = set(override.get("recommend", []))
    ignore = set(override.get("ignore", []))

    out: dict[str, str] = {}
    for field, freq in baseline_freq.items():
        if field in ignore:
            continue
        out[field] = tier_for(freq)

    for field in required:
        if field in ignore:
            continue
        out[field] = "required"
    for field in recommend:
        if field in ignore:
            continue
        if out.get(field) != "required":
            out[field] = "expected"

    return out


_BYTYPE_ALTERNATIVES: dict[str, str] = {
    "nutritionProps": "nutritionPropsByType",
    "nutritionProps.satiety": "nutritionPropsByType",
    "nutritionProps.foodcategory": "nutritionPropsByType",
    "transitionableProps": "transitionablePropsByType",
    "creativeinventory": "creativeinventoryByType",
}


def _has_field_or_bytype(profile: FoodProfile, field: str) -> bool:
    """Check field, falling back to its ByType alternative if one exists."""
    if profile.has_field(field):
        return True
    alt = _BYTYPE_ALTERNATIVES.get(field)
    return alt is not None and profile.has_field(alt)


def check_food_schema(
    profile: FoodProfile,
    baseline: Mapping[str, Mapping[str, float]],
    overrides: Mapping[str, Mapping[str, list[str]]],
    result: ValidationResult,
) -> None:
    category = profile.foodcategory or "NoNutrition"
    category_baseline = baseline.get(category, {})
    category_override = overrides.get(category, {"require": [], "recommend": [], "ignore": []})

    effective = merge_tiers(category_baseline, category_override)

    for field, tier in effective.items():
        if _has_field_or_bytype(profile, field):
            continue
        msg = f"[{profile.code}] {category} missing {tier} field: {field}"
        if tier == "required":
            result.error(profile.source_file, msg, rule_id="food.schema")
        elif tier == "expected":
            result.warning(profile.source_file, msg, rule_id="food.schema")
