"""Migrated asset validators.

Pure refactor of the original validate-assets.py module body. Each function
takes a ValidationContext instead of the old `indexes` dict. Print output is
unchanged so existing CI consumers keep working.
"""
from __future__ import annotations

import re
from pathlib import Path

from vs_validators.common import (
    C, load_json5, rel, err, warn, ok, info, header, ValidationResult,
)
from vs_validators.context import ValidationContext
from vs_validators.indexes import load_lang_raw


def find_json_files(directory):
    """Recursively find all .json files under a directory."""
    if not directory.exists():
        return []
    return sorted(directory.rglob("*.json"))


def resolve_shape_ref(shape_base, ctx: ValidationContext):
    """Check if a shape reference resolves. Returns (resolved, message).
    Only checks seafarer: domain shapes. game: shapes are assumed valid.
    """
    if not shape_base:
        return True, None

    if shape_base.startswith("seafarer:"):
        path = shape_base[len("seafarer:"):]
        if path in ctx.seafarer_shapes:
            return True, None
        return False, f"Shape not found: {shape_base}"

    if shape_base.startswith("game:"):
        return True, None  # Can't validate game assets

    # No prefix = current domain (seafarer)
    if shape_base in ctx.seafarer_shapes:
        return True, None
    return False, f"Shape not found (no domain prefix): {shape_base}"


def resolve_texture_ref(tex_base, ctx: ValidationContext):
    """Check if a texture reference resolves (seafarer: domain only)."""
    if not tex_base:
        return True, None

    if tex_base.startswith("seafarer:"):
        path = tex_base[len("seafarer:"):]
        if path in ctx.seafarer_textures:
            return True, None
        # Check with variant wildcards stripped
        clean = re.sub(r'\{[^}]+\}', '*', path)
        for t in ctx.seafarer_textures:
            if re.match(clean.replace('*', '.*'), t):
                return True, None
        return False, f"Texture not found: {tex_base}"

    if tex_base.startswith("game:"):
        return True, None

    # No prefix
    if tex_base in ctx.seafarer_textures:
        return True, None
    return True, None  # Be lenient for unprefixed


def extract_shape_bases(data):
    """Recursively extract all shape.base values from a dict."""
    bases = []
    if isinstance(data, dict):
        if "shape" in data and isinstance(data["shape"], dict):
            b = data["shape"].get("base")
            if b:
                bases.append(b)
        # Check shapeByType and shapebytype
        for key in ("shapeByType", "shapebytype", "shapesbytype"):
            if key in data and isinstance(data[key], dict):
                for variant, shape_obj in data[key].items():
                    if isinstance(shape_obj, dict):
                        b = shape_obj.get("base")
                        if b:
                            bases.append(b)
                    elif isinstance(shape_obj, str):
                        bases.append(shape_obj)
        for v in data.values():
            bases.extend(extract_shape_bases(v))
    elif isinstance(data, list):
        for item in data:
            bases.extend(extract_shape_bases(item))
    return bases


def extract_texture_bases(data):
    """Recursively extract texture base paths from a dict."""
    bases = []
    if isinstance(data, dict):
        if "base" in data and isinstance(data["base"], str):
            val = data["base"]
            # Heuristic: texture refs contain slashes and look like paths
            if "/" in val and not val.startswith("block/food/meal"):
                bases.append(val)
        for v in data.values():
            bases.extend(extract_texture_bases(v))
    elif isinstance(data, list):
        for item in data:
            bases.extend(extract_texture_bases(item))
    return bases


def validate_blocktypes(result: ValidationResult, ctx: ValidationContext, verbose: bool):
    """Validate all blocktype JSON files."""
    files = find_json_files(ctx.seafarer / "blocktypes")
    print(header(f"Blocktypes ({len(files)} files)"))

    for f in files:
        data, error = load_json5(f)
        if error:
            result.error(f, f"JSON parse error: {error}", rule_id="asset.blocktypes")
            print(err(f"{rel(f, ctx.assets_root)}: {error}"))
            continue

        # Must have code
        if isinstance(data, dict):
            if "code" not in data:
                result.error(f, "Missing 'code' field", rule_id="asset.blocktypes")
                print(err(f"{rel(f, ctx.assets_root)}: missing 'code'"))
            else:
                result.ok()
                if verbose:
                    print(ok(f"{rel(f, ctx.assets_root)}: code={data['code']}"))

            # Check shape references
            for shape_base in extract_shape_bases(data):
                resolved, msg = resolve_shape_ref(shape_base, ctx)
                if not resolved:
                    result.warning(f, msg, rule_id="asset.blocktypes")
                    print(warn(f"{rel(f, ctx.assets_root)}: {msg}"))

            # Check texture references (seafarer domain only, verbose-only since
            # many textures are embedded in shape files rather than standalone .png)
            if verbose:
                for tex_base in extract_texture_bases(data):
                    resolved, msg = resolve_texture_ref(tex_base, ctx)
                    if not resolved:
                        print(f"         {C.DIM}texture: {msg}{C.RESET}")
        else:
            result.error(f, "Expected object, got array or other type", rule_id="asset.blocktypes")
            print(err(f"{rel(f, ctx.assets_root)}: expected object at top level"))


def validate_itemtypes(result: ValidationResult, ctx: ValidationContext, verbose: bool):
    """Validate all itemtype JSON files."""
    files = find_json_files(ctx.seafarer / "itemtypes")
    print(header(f"Itemtypes ({len(files)} files)"))

    for f in files:
        data, error = load_json5(f)
        if error:
            result.error(f, f"JSON parse error: {error}", rule_id="asset.itemtypes")
            print(err(f"{rel(f, ctx.assets_root)}: {error}"))
            continue

        if isinstance(data, dict):
            if "code" not in data:
                result.error(f, "Missing 'code' field", rule_id="asset.itemtypes")
                print(err(f"{rel(f, ctx.assets_root)}: missing 'code'"))
            else:
                result.ok()
                if verbose:
                    print(ok(f"{rel(f, ctx.assets_root)}: code={data['code']}"))

            for shape_base in extract_shape_bases(data):
                resolved, msg = resolve_shape_ref(shape_base, ctx)
                if not resolved:
                    result.warning(f, msg, rule_id="asset.itemtypes")
                    print(warn(f"{rel(f, ctx.assets_root)}: {msg}"))

            if verbose:
                for tex_base in extract_texture_bases(data):
                    resolved, msg = resolve_texture_ref(tex_base, ctx)
                    if not resolved:
                        print(f"         {C.DIM}texture: {msg}{C.RESET}")
        else:
            result.error(f, "Expected object, got array or other type", rule_id="asset.itemtypes")
            print(err(f"{rel(f, ctx.assets_root)}: expected object at top level"))


def validate_recipes(result: ValidationResult, ctx: ValidationContext, verbose: bool):
    """Validate all recipe files across subtypes."""
    recipe_dir = ctx.seafarer / "recipes"
    files = find_json_files(recipe_dir)
    print(header(f"Recipes ({len(files)} files)"))

    for f in files:
        data, error = load_json5(f)
        if error:
            result.error(f, f"JSON parse error: {error}", rule_id="asset.recipes")
            print(err(f"{rel(f, ctx.assets_root)}: {error}"))
            continue

        # Recipes can be a single object or array of objects
        recipes = data if isinstance(data, list) else [data]
        subtype = f.parent.name  # barrel, grid, cooking, etc.

        for i, recipe in enumerate(recipes):
            label = f"{rel(f, ctx.assets_root)}[{i}]" if len(recipes) > 1 else rel(f, ctx.assets_root)

            if not isinstance(recipe, dict):
                result.error(f, f"Recipe {i} is not an object", rule_id="asset.recipes")
                print(err(f"{label}: recipe is not an object"))
                continue

            # Grid recipes need ingredientPattern + ingredients + output
            if subtype == "grid":
                for field in ("ingredientPattern", "ingredients", "output"):
                    if field not in recipe:
                        result.warning(f, f"Grid recipe missing '{field}'", rule_id="asset.recipes")
                        print(warn(f"{label}: missing '{field}'"))

                # Validate pattern chars match ingredient keys
                pattern = recipe.get("ingredientPattern", "")
                ingredients = recipe.get("ingredients", {})
                if pattern and ingredients:
                    pattern_chars = set(re.findall(r'[A-Z]', pattern))
                    ingredient_keys = set(ingredients.keys())
                    missing = pattern_chars - ingredient_keys
                    extra = ingredient_keys - pattern_chars
                    if missing:
                        result.error(f, f"Pattern uses {missing} but no matching ingredient", rule_id="asset.recipes")
                        print(err(f"{label}: pattern references undefined ingredients: {missing}"))
                    if extra:
                        result.warning(f, f"Ingredients {extra} not used in pattern", rule_id="asset.recipes")
                        print(warn(f"{label}: unused ingredients: {extra}"))

            # Barrel recipes need ingredients + output
            elif subtype == "barrel":
                if "ingredients" not in recipe:
                    result.warning(f, f"Barrel recipe missing 'ingredients'", rule_id="asset.recipes")
                    print(warn(f"{label}: missing 'ingredients'"))
                if "output" not in recipe:
                    result.warning(f, f"Barrel recipe missing 'output'", rule_id="asset.recipes")
                    print(warn(f"{label}: missing 'output'"))

            # Cooking recipes need ingredients
            elif subtype == "cooking":
                if "ingredients" not in recipe:
                    result.warning(f, f"Cooking recipe missing 'ingredients'", rule_id="asset.recipes")
                    print(warn(f"{label}: missing 'ingredients'"))

            # Check all ingredient codes have domain prefix
            def check_codes(obj, path=""):
                if isinstance(obj, dict):
                    code = obj.get("code", "")
                    if code and ":" not in code and "/" not in code:
                        # Bare code with no domain — could be intentional for wildcard matching
                        pass
                    for k, v in obj.items():
                        check_codes(v, f"{path}.{k}")
                elif isinstance(obj, list):
                    for j, item in enumerate(obj):
                        check_codes(item, f"{path}[{j}]")

            check_codes(recipe)
            result.ok()
            if verbose:
                code = recipe.get("code", "unnamed")
                print(ok(f"{label}: {subtype} recipe '{code}'"))


def validate_patches(result: ValidationResult, ctx: ValidationContext, verbose: bool):
    """Validate patch files."""
    files = find_json_files(ctx.seafarer / "patches") + find_json_files(ctx.game / "patches")
    print(header(f"Patches ({len(files)} files)"))

    for f in files:
        data, error = load_json5(f)
        if error:
            result.error(f, f"JSON parse error: {error}", rule_id="asset.patches")
            print(err(f"{rel(f, ctx.assets_root)}: {error}"))
            continue

        patches = data if isinstance(data, list) else [data]

        for i, patch in enumerate(patches):
            label = f"{rel(f, ctx.assets_root)}[{i}]" if len(patches) > 1 else rel(f, ctx.assets_root)

            if not isinstance(patch, dict):
                result.error(f, f"Patch {i} is not an object", rule_id="asset.patches")
                print(err(f"{label}: patch is not an object"))
                continue

            # Required fields
            op = patch.get("op")
            if not op:
                result.error(f, "Patch missing 'op'", rule_id="asset.patches")
                print(err(f"{label}: missing 'op'"))
            elif op not in ("add", "addmerge", "addeach", "remove", "replace", "copy", "move", "test"):
                result.warning(f, f"Unusual op: '{op}'", rule_id="asset.patches")
                print(warn(f"{label}: unusual op '{op}'"))

            if "path" not in patch:
                result.error(f, "Patch missing 'path'", rule_id="asset.patches")
                print(err(f"{label}: missing 'path'"))

            if "file" not in patch:
                result.error(f, "Patch missing 'file'", rule_id="asset.patches")
                print(err(f"{label}: missing 'file'"))
            else:
                file_ref = patch["file"]
                # Validate domain prefix
                if ":" not in file_ref:
                    result.warning(f, f"Patch file ref missing domain prefix: '{file_ref}'", rule_id="asset.patches")
                    print(warn(f"{label}: file ref missing domain: '{file_ref}'"))

            if op not in ("remove", "copy", "move") and "value" not in patch:
                result.warning(f, f"Patch op='{op}' has no 'value'", rule_id="asset.patches")
                print(warn(f"{label}: op '{op}' has no 'value'"))

            # Check for condition/side fields (valid but worth noting)
            result.ok()
            if verbose:
                target = patch.get("file", "?")
                print(ok(f"{label}: {op} -> {target}"))


def validate_lang(result: ValidationResult, ctx: ValidationContext, verbose: bool):
    """Validate lang files and check for missing entries."""
    lang_file = ctx.seafarer / "lang" / "en.json"
    print(header("Lang"))

    if not lang_file.exists():
        result.error(lang_file, "en.json not found", rule_id="asset.lang")
        print(err("seafarer/lang/en.json not found"))
        return

    if ctx.lang_keys:
        data = ctx.lang_keys
    else:
        data, error = load_lang_raw(lang_file)
        if error:
            result.error(lang_file, f"JSON parse error: {error}", rule_id="asset.lang")
            print(err(f"en.json: {error}"))
            return

    # Check for empty values
    empty_keys = [k for k, v in data.items() if not v or (isinstance(v, str) and not v.strip())]
    if empty_keys:
        for k in empty_keys:
            result.warning(lang_file, f"Empty lang value for key '{k}'", rule_id="asset.lang")
            print(warn(f"Empty lang value: '{k}'"))

    # Collect all block/item codes to check for lang entries
    block_codes = set()
    for f in find_json_files(ctx.seafarer / "blocktypes"):
        d, _ = load_json5(f)
        if d and isinstance(d, dict) and "code" in d:
            block_codes.add(d["code"])

    item_codes = set()
    for f in find_json_files(ctx.seafarer / "itemtypes"):
        d, _ = load_json5(f)
        if d and isinstance(d, dict) and "code" in d:
            item_codes.add(d["code"])

    # Check that blocks/items have lang entries
    # VS lang keys follow patterns like: block-{code}-{variant}, item-{code}, etc.
    lang_keys_lower = {k.lower() for k in data.keys()}

    missing_block_lang = []
    for code in sorted(block_codes):
        # Check for any key containing the block code
        found = any(code in k for k in lang_keys_lower)
        if not found:
            missing_block_lang.append(code)

    missing_item_lang = []
    for code in sorted(item_codes):
        found = any(code in k for k in lang_keys_lower)
        if not found:
            missing_item_lang.append(code)

    if missing_block_lang:
        print(warn(f"Blocks with no lang entries ({len(missing_block_lang)}):"))
        for code in missing_block_lang:
            result.warning(lang_file, f"No lang entry for block '{code}'", rule_id="asset.lang")
            print(f"         {C.DIM}{code}{C.RESET}")

    if missing_item_lang:
        print(warn(f"Items with no lang entries ({len(missing_item_lang)}):"))
        for code in missing_item_lang:
            result.warning(lang_file, f"No lang entry for item '{code}'", rule_id="asset.lang")
            print(f"         {C.DIM}{code}{C.RESET}")

    total_keys = len(data)
    result.ok()
    print(ok(f"en.json: {total_keys} lang keys loaded"))
    if verbose:
        print(info(f"Block codes: {len(block_codes)}, Item codes: {len(item_codes)}"))


def validate_config(result: ValidationResult, ctx: ValidationContext, verbose: bool):
    """Validate config files (handbook, dialogue, tradelists)."""
    files = find_json_files(ctx.seafarer / "config")
    print(header(f"Config ({len(files)} files)"))

    for f in files:
        data, error = load_json5(f)
        if error:
            result.error(f, f"JSON parse error: {error}", rule_id="asset.config")
            print(err(f"{rel(f, ctx.assets_root)}: {error}"))
            continue

        result.ok()
        if verbose:
            print(ok(f"{rel(f, ctx.assets_root)}"))


def validate_entities(result: ValidationResult, ctx: ValidationContext, verbose: bool):
    """Validate entity files."""
    files = find_json_files(ctx.seafarer / "entities") + find_json_files(ctx.game / "entities")
    print(header(f"Entities ({len(files)} files)"))

    for f in files:
        data, error = load_json5(f)
        if error:
            result.error(f, f"JSON parse error: {error}", rule_id="asset.entities")
            print(err(f"{rel(f, ctx.assets_root)}: {error}"))
            continue

        if isinstance(data, dict):
            if "code" not in data:
                result.error(f, "Missing 'code' field", rule_id="asset.entities")
                print(err(f"{rel(f, ctx.assets_root)}: missing 'code'"))
            else:
                result.ok()
                if verbose:
                    print(ok(f"{rel(f, ctx.assets_root)}: code={data['code']}"))

            for shape_base in extract_shape_bases(data):
                resolved, msg = resolve_shape_ref(shape_base, ctx)
                if not resolved:
                    result.warning(f, msg, rule_id="asset.entities")
                    print(warn(f"{rel(f, ctx.assets_root)}: {msg}"))
