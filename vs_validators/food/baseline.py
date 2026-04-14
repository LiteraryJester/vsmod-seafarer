"""Compute schema field-frequency baseline per food category."""
from __future__ import annotations

from collections import Counter, defaultdict

from vs_validators.context import FoodProfile


REQUIRED_THRESHOLD = 0.95
EXPECTED_THRESHOLD = 0.50


def tier_for(frequency: float) -> str:
    if frequency >= REQUIRED_THRESHOLD:
        return "required"
    if frequency >= EXPECTED_THRESHOLD:
        return "expected"
    return "optional"


def _flatten_fields(profile: FoodProfile) -> set[str]:
    """Return dotted-path field names present in the raw dict.
    Top-level keys, attributes.*, and nutritionProps.* subkeys."""
    fields: set[str] = set(profile.fields_present)
    for attr in profile.attributes_present:
        fields.add(f"attributes.{attr}")
    np = profile.raw.get("nutritionProps")
    if isinstance(np, dict):
        for k in np.keys():
            fields.add(f"nutritionProps.{k}")
    return fields


def compute_baseline(profiles: dict[str, FoodProfile]) -> dict[str, dict[str, float]]:
    """Return baseline[category][field] = frequency in [0.0, 1.0]."""
    bucketed: dict[str, list[set[str]]] = defaultdict(list)
    for profile in profiles.values():
        if profile.foodcategory is None:
            continue
        bucketed[profile.foodcategory].append(_flatten_fields(profile))

    baseline: dict[str, dict[str, float]] = {}
    for category, items in bucketed.items():
        if not items:
            continue
        counter: Counter = Counter()
        for field_set in items:
            for f in field_set:
                counter[f] += 1
        total = len(items)
        baseline[category] = {k: v / total for k, v in counter.items()}
    return baseline
