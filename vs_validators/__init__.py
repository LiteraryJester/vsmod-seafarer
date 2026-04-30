"""Seafarer mod asset validators."""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

from vs_validators.common import ValidationResult, C, header, info, err

__all__ = ["ValidationResult", "run"]


DEFAULT_BASE_GAME = Path("/mnt/d/Development/vs/assets")
DEFAULT_EF = Path("/mnt/d/Development/vs/existing mods/ExpandedFoods 2.0.0-dev.7/assets")


def _default_assets_root() -> Path:
    return Path(__file__).resolve().parents[1] / "Seafarer" / "Seafarer" / "assets"


def run(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Validate Seafarer mod assets")
    parser.add_argument("-v", "--verbose", action="store_true")
    parser.add_argument(
        "-t", "--type", default="all",
        choices=["all", "blocktypes", "itemtypes", "recipes", "patches",
                 "lang", "config", "entities", "food"],
    )
    parser.add_argument("-r", "--rule", default="")
    parser.add_argument("--skip-rule", default="")
    parser.add_argument("-f", "--file", default="")
    parser.add_argument("--strict", action="store_true")
    parser.add_argument("--dump-baseline", action="store_true")
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--base-game", type=Path, default=DEFAULT_BASE_GAME)
    parser.add_argument("--ef-path", type=Path, default=DEFAULT_EF)
    args = parser.parse_args(argv)

    from vs_validators.context import build_context
    from vs_validators import asset_types

    assets_root = _default_assets_root()
    if not assets_root.exists():
        print(err(f"Assets root not found: {assets_root}"))
        return 2

    print(f"{C.BOLD}Seafarer Mod Asset Validator{C.RESET}")
    print(f"Assets root: {assets_root}")

    load_food = args.type in ("all", "food") or args.dump_baseline
    ctx = build_context(
        assets_root=assets_root,
        base_game_assets=args.base_game if args.base_game.exists() else None,
        ef_assets=args.ef_path if args.ef_path.exists() else None,
        load_food=load_food,
    )
    print(info(f"Indexed {len(ctx.seafarer_shapes)} shapes, "
               f"{len(ctx.seafarer_textures)} textures"))

    result = ValidationResult()
    t = args.type

    if t in ("all", "blocktypes"):
        asset_types.validate_blocktypes(result, ctx, args.verbose)
    if t in ("all", "itemtypes"):
        asset_types.validate_itemtypes(result, ctx, args.verbose)
    if t in ("all", "recipes"):
        asset_types.validate_recipes(result, ctx, args.verbose)
    if t in ("all", "patches"):
        asset_types.validate_patches(result, ctx, args.verbose)
    if t in ("all", "lang"):
        asset_types.validate_lang(result, ctx, args.verbose)
    if t in ("all", "config"):
        asset_types.validate_config(result, ctx, args.verbose)
    if t in ("all", "entities"):
        asset_types.validate_entities(result, ctx, args.verbose)
    if t in ("all", "food"):
        try:
            from vs_validators.food import validate_food
            validate_food(result, ctx, args)
        except ImportError:
            if t == "food":
                print(err("food validator not yet installed"))
                return 2
            # For --type all, silently skip until Task 12 wires it up.

    print(header("Summary"))
    print(f"  Passed:   {C.GREEN}{result.passed}{C.RESET}")
    print(f"  Warnings: {C.YELLOW}{len(result.warnings)}{C.RESET}")
    print(f"  Errors:   {C.RED}{len(result.errors)}{C.RESET}")

    if args.strict and result.warnings:
        print(f"\n{C.RED}{C.BOLD}FAILED (strict){C.RESET} — warnings treated as errors")
        return 1

    if result.errors:
        print(f"\n{C.RED}{C.BOLD}FAILED{C.RESET} — {len(result.errors)} error(s)")
        return 1
    if result.warnings:
        print(f"\n{C.YELLOW}{C.BOLD}PASSED with warnings{C.RESET}")
        return 0
    print(f"\n{C.GREEN}{C.BOLD}ALL CLEAR{C.RESET}")
    return 0
