"""Food validator orchestrator."""
from __future__ import annotations

import fnmatch
from argparse import Namespace

from vs_validators.common import ValidationResult, Finding, header, info, err, warn, C
from vs_validators.context import ValidationContext
from vs_validators.food.baseline import compute_baseline
from vs_validators.food.schema import load_overrides, check_food_schema
from vs_validators.food.capabilities import check_pie_support, check_burrito_support
from vs_validators.food.ef import check_ef_protein_variants, check_ef_recipe_coverage
from vs_validators.food.alcohol import check_brewing_coverage
from vs_validators.food.lang import check_recipe_driven_lang, check_category_driven_lang


RULES = {
    "food.schema":       "schema check",
    "food.pie":          "pie support",
    "food.burrito":      "burrito support",
    "food.ef_protein":   "EF protein variants",
    "food.ef_coverage":  "EF recipe coverage",
    "food.alcohol":      "brewing coverage",
    "food.lang_recipe":  "recipe-driven lang",
    "food.lang_category": "category-driven lang",
}

# Food codes whose warnings are intentionally suppressed because their design
# diverges from the general patterns (errors are still reported).
WARNING_EXCLUSIONS: set[str] = {
    "saltrubbedmeat",
    "coconut",
    "coconutmeat",
    "coconutpressedmash",
    "corn",
    "driedcoconut",
    "driedsaltedmeat",
    "flatbread",
    "liveclam",
    "driedcorn",
    "nixtamal",
}


def _active_rules(args: Namespace) -> set[str]:
    all_ids = set(RULES.keys())
    if args.rule:
        requested = {r.strip() for r in args.rule.split(",") if r.strip()}
        active = all_ids & requested
    else:
        active = set(all_ids)
    if args.skip_rule:
        skipped = {r.strip() for r in args.skip_rule.split(",") if r.strip()}
        active -= skipped
    return active


def _matches_file_filter(profile_name: str, pattern: str) -> bool:
    if not pattern:
        return True
    return fnmatch.fnmatch(profile_name, pattern) or fnmatch.fnmatch(profile_name, f"*{pattern}*")


def validate_food(result: ValidationResult, ctx: ValidationContext, args: Namespace) -> None:
    """Run all food rules against ctx.mod_food."""
    print(header(f"Food ({len(ctx.mod_food)} items)"))

    merged_for_baseline = {**ctx.base_game_food, **ctx.ef_food}
    ctx.schema_baseline = compute_baseline(merged_for_baseline)
    ctx.schema_overrides = load_overrides()

    if getattr(args, "dump_baseline", False):
        print(info("Schema baseline (category -> field -> frequency):"))
        for cat, fields in sorted(ctx.schema_baseline.items()):
            print(f"  {C.BOLD}{cat}{C.RESET}")
            for field, freq in sorted(fields.items(), key=lambda x: -x[1]):
                print(f"    {freq:.2f}  {field}")
        return

    active = _active_rules(args)

    for code, profile in sorted(ctx.mod_food.items()):
        if not _matches_file_filter(profile.source_file.name, args.file):
            continue

        before_count = len(result.findings)

        if "food.schema" in active:
            check_food_schema(profile, ctx.schema_baseline, ctx.schema_overrides, result)
        if "food.pie" in active:
            check_pie_support(profile, ctx, result)
        if "food.burrito" in active:
            check_burrito_support(profile, ctx, result)
        if "food.ef_protein" in active:
            check_ef_protein_variants(profile, ctx, result)
        if "food.ef_coverage" in active:
            check_ef_recipe_coverage(profile, ctx, result)
        if "food.alcohol" in active:
            check_brewing_coverage(profile, ctx, result)
        if "food.lang_recipe" in active:
            check_recipe_driven_lang(profile, ctx, result)
        if "food.lang_category" in active:
            check_category_driven_lang(profile, ctx, result)

        if code in WARNING_EXCLUSIONS:
            result.findings[before_count:] = [
                f for f in result.findings[before_count:] if f.severity != "warning"
            ]

        new_findings = result.findings[before_count:]
        for finding in new_findings:
            tag = f"{C.DIM}[{finding.rule_id}]{C.RESET} " if finding.rule_id else ""
            if finding.severity == "error":
                print(err(f"{tag}{finding.message}"))
            else:
                print(warn(f"{tag}{finding.message}"))

        if not new_findings:
            result.ok()
            if getattr(args, "verbose", False):
                print(f"  {C.GREEN}OK{C.RESET} {code}")
