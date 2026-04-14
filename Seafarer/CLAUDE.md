# Salt and Sand - Vintage Story Mod

## Project Overview
A Vintage Story mod (modid: `seafarer`) that started as a log barge/raft mod and is expanding in scope. Currently adds a logbarge boat type with accessory slots, raft sails, and storage expansion points.

## Build & Run
- **Build**: `dotnet build` from repo root, or use `build.ps1` / `build.sh` (Cake build)
- **Target**: .NET 10.0
- **Output**: `SaltAndSand/bin/Debug/Mods/mod/` (copied to VS mod folder)
- **Dependencies**: Vintage Story install at `$(VINTAGE_STORY)` env var — references VintagestoryAPI, VSSurvivalMod, VSEssentials, VSCreativeMod, VintagestoryLib, Harmony, protobuf-net, cairo-sharp, Newtonsoft.Json, Microsoft.Data.Sqlite
- **Game version**: 1.21.0+

## Project Structure
```
SaltAndSand/
├── SaltAndSand/                    # Main mod project
│   ├── SaltAndSandModSystem.cs     # Entry point (ModSystem subclass)
│   ├── modinfo.json                # Mod metadata
│   ├── assets/
│   │   ├── seafarer/            # Mod-namespaced assets
│   │   │   ├── entities/           # Entity definitions (JSON5)
│   │   │   ├── itemtypes/          # Item type definitions
│   │   │   ├── recipes/grid/       # Crafting recipes
│   │   │   ├── shapes/             # 3D model shapes
│   │   │   ├── config/handbook/    # Handbook guide entries
│   │   │   └── lang/en.json        # English translations
│   │   └── game/patches/           # JSON patches to base game data
│   └── SaltAndSand.csproj
├── ZZCakeBuild/                    # Cake build system
└── SaltAndSand.sln
```

## Vintage Story Modding Conventions

### Asset Prefixing
- **CRITICAL**: When referencing base game assets from mod JSON, always use the `game:` prefix
  - Example: `"game:block/wood/debarked/{material}"`, `"game:item/resource/rope"`, `"game:log-placed-{material}-ud"`
- Mod's own assets use `seafarer:` prefix (or no prefix within the mod's own namespace)
- Other domain prefixes exist: `game:` (core), `survival:` (survival mod), `creative:` (creative mod)
- **Mod prefixes for optional compat**: `butchering:`, `expandedfoods:`, `aculinaryartillery:` — use when referencing or patching those mods' assets (always with `dependsOn`)

### JSON Format
- VS uses **JSON5** (relaxed JSON) for asset files — trailing commas, unquoted keys, and comments are allowed
- Entity, block, and item definitions follow VS schema conventions

### Key VS API Patterns
- Mod entry point: class extending `ModSystem` with `Start()`, `StartServerSide()`, `StartClientSide()`
- Register custom classes with `api.Register*Class()` methods
- Use `Mod.Logger` for logging, `Lang.Get()` for translations
- Entity behaviors are composable (e.g., `passivephysicsmultibox`, `creaturecarrier`, `rideableaccessories`)

### Variant System
- `variantgroups` define variants with `code` and `states`
- `loadFromProperties` can pull states from shared property files (e.g., `"block/wood"`)
- Use `{variantcode}` in paths/codes for substitution (e.g., `{material}`)
- Type-specific overrides use `*ByType` suffix with wildcard patterns (e.g., `shapeByType`, `texturesByType`)

### Patches
- JSON patches in `assets/game/patches/` modify base game data
- Used to extend existing game items/blocks (e.g., adding barrel/lamp compatibility with logbarge)
- **Conditional patches**: Use `dependsOn: [{ "modid": "modname" }]` to apply patches only when a mod is installed
- **Cross-mod patching**: Target other mods with `"file": "modid:path/to/asset.json"`

### Mod Dependencies
- **Required**: base game `1.22.0-rc.7`, `progressionframework 1.0.0` (hard dependency — Seafarer's training, quest, and evolving-trader systems all live in the framework mod; see `D:\Development\vs\vsmod-progression-framework\`)
- **Optional compat**: `expandedfoods` (enables salted/dried meat items & recipes via `dependsOn` patches), `butchering`, `hydrateordiedrate`, `craftablecartography`, `carryon`, `cartwrightscaravan`
- **Conditional content pattern**: Set `enabled: false` on items/recipes by default, then use a self-patch with `dependsOn` to enable them when the optional mod is present (see `patches/expandedfoods-meats.json`)

### ProgressionFramework integration
- Training definitions live at `assets/seafarer/config/training/` (professions, xp, config) — discovered by the framework's cross-domain scanner.
- Quest definitions at `assets/seafarer/config/quests/*.json`; tradelists at `assets/seafarer/config/tradelists/*.json`; dialogue at `assets/seafarer/config/dialogue/*.json`.
- Trader entities (Drake, Morgan, Celeste, Reva, etc.) use `"class": "EntityEvolvingTrader"` — the class itself is registered by ProgressionFramework at Start.
- Training book items (`seafarer:trainingbook-*`) use `"class": "ItemTrainingBook"` — same pattern.
- Salt-and-sand's csproj has a `ProjectReference` to the framework csproj; framework types are resolvable at compile time.

## Reference Resources (local paths)
- **VintagestoryAPI source**: `D:\Development\vs\vsapi\` — canonical interfaces (`ICoreAPI`, `ModSystem`, `Entity`, etc.). WSL: `/mnt/d/Development/vs/vsapi/`.
- **VSSurvivalMod source**: `D:\Development\vs\vssurvivalmod\` — base game content implementations (`EntityTrader`, `BehaviorConversable`, `GridRecipe`, etc.). WSL: `/mnt/d/Development/vs/vssurvivalmod/`.
- **Extracted game assets**: `D:\Development\vs\assets\` — WSL: `/mnt/d/Development/vs/assets/`.
  - `game/` — core game data (blocktypes, entities, shapes, textures, sounds, lang, config)
  - `survival/` — survival mod data (blocktypes, itemtypes, entities, recipes, worldgen, patches)
  - `creative/` — creative mod data
- **Popular community mods**: `D:\Development\vs\existing mods\` — WSL: `/mnt/d/Development/vs/existing mods/` — reference implementations for compat, patching, and code organization.

## Upstream references
- **VS API (GitHub)**: https://github.com/anegostudios/vsapi
- **VS Survival Mod (GitHub)**: https://github.com/anegostudios/vssurvivalmod
- **VS Essentials Mod (GitHub)**: https://github.com/anegostudios/vsessentialsmod

## Known Bugs
- **Exposure resets to 0 on world load**: The persisted exposure level, condition, tier, and timestamp are lost when the player reloads the world. The `WatchedAttributes` tree for `"exposure"` appears to not survive save/load despite following the same pattern as the base game's `BehaviorBodyTemperature`. Needs investigation into whether the data is not being saved, or is being overwritten during entity deserialization. See `EntityBehaviorExposure.cs`.

## Current Features
- **Log Barge**: Large raft entity with 2 seats, 6 expansion slots (port/starboard x fore/mid/aft), oar storage, sail mount
- **Raft Sail**: Wearable item for the sail slot
- **Accessory System**: Slots accept chests, baskets, storage vessels, lanterns, oil lamps, barrels, rope tie posts
- **Recipes**: Grid crafting for logbarge and raft accessories

## Validation

This project has Python validators under `vs_validators/`. Run them before
claiming work is complete.

### Asset validation
Run after changes to any `assets/` files:

    python3 validate-assets.py

Exit 0 with 0 errors means OK. Warnings acceptable if justified in the commit
message.

### Food validation
Run after changes to files under:
  - `SaltAndSand/assets/seafarer/itemtypes/food/`
  - `SaltAndSand/assets/seafarer/patches/expandedfoods-*.json`
  - `SaltAndSand/assets/seafarer/patches/brewing.json`
  - `SaltAndSand/assets/seafarer/lang/en.json`

    python3 validate-food.py

Rule IDs are `food.<rule>`. To run a subset:

    python3 validate-food.py --rule food.pie,food.burrito
    python3 validate-food.py --file chili.json
    python3 validate-food.py --skip-rule food.lang_category

Errors must be fixed before claiming completion. Warnings are judgment calls —
if a warning is intentional, note it in the commit message.

### Dependencies
Validators need `json5` and `pyyaml`:

    pip install json5 pyyaml
