# Seafarer migration + restructure — design

**Date:** 2026-04-14
**Status:** Draft — awaiting user review before implementation planning

## Goal

Migrate the Seafarer mod from `vsmod-salt-and-sand` (project folder `SaltAndSand/`, namespace `SaltAndSand`) into a new repo `vsmod-seafarer` under `Seafarer/`, renaming the project + namespace to match the modid (`seafarer`), reorganizing the C# source into type-based folders modelled on VSSurvivalMod, and cleaning up one typo-level asset duplication. This is a pure rename + restructure; no behavior changes.

## Non-goals

- Preserving git history. The old repo stays as a historical reference; new repo starts fresh.
- Behavior changes. Every block, item, entity, recipe, and config keeps the same runtime semantics.
- Deleting `vsmod-salt-and-sand`. It remains on disk as archive.
- Content or balance changes.

## Decisions

- **Scope** = full reorganization: project rename + C# reorg + asset dedupe.
- **Namespace rename** `SaltAndSand` → `Seafarer`.
- **Modid unchanged** — stays `seafarer`.
- **Fresh git history** in `vsmod-seafarer`; salt-and-sand retired.
- **Flat namespace**, not folder-nested. All files declare `namespace Seafarer` regardless of subfolder, matching VSSurvivalMod's convention (`Block/BlockStairs.cs` declares `namespace Vintagestory.GameContent`, not `.Block`).

## Repo layout after migration

```
vsmod-seafarer/
├── LICENSE
└── Seafarer/                              # solution root
    ├── Seafarer.sln
    ├── Seafarer.slnx
    ├── build.ps1 / build.sh
    ├── ZZCakeBuild/
    ├── CLAUDE.md                          # migrated from SaltAndSand/CLAUDE.md
    ├── docs/                              # from salt-and-sand/SaltAndSand/docs/
    └── Seafarer/                          # C# project
        ├── Seafarer.csproj
        ├── SeafarerModSystem.cs
        ├── modinfo.json                   # modid stays "seafarer"
        ├── Properties/
        ├── Block/                         # 10 files
        ├── BlockEntity/                   # 8 files
        ├── CollectibleBehavior/           # 4 files
        ├── Entity/                        # 1 file
        ├── EntityBehavior/                # 3 files (exposure + helpers)
        ├── Item/                          # 1 file
        ├── Recipe/                        # 4 files (griddle + preptable recipe + registry)
        ├── Config/                        # 4 files (DryingFrame, Griddle, SaltPan, MudRake)
        ├── assets/                        # cleaned-up asset tree
        └── quests/                        # design docs (*.md) only
```

## C# file organization

| Folder | Files |
|---|---|
| `Block/` | `BlockAmphora.cs`, `BlockAmphoraStorage.cs`, `BlockBurrito.cs`, `BlockDryingFrame.cs`, `BlockGriddle.cs`, `BlockGriddleHearth.cs`, `BlockGriddleHearthBase.cs`, `BlockOpenCoconut.cs`, `BlockPrepTable.cs`, `BlockSaltPan.cs` |
| `BlockEntity/` | `BlockEntityAmphoraStorage.cs`, `BlockEntityBurrito.cs`, `BlockEntityDryingFrame.cs`, `BlockEntityGriddleHearth.cs`, `BlockEntityOpenCoconut.cs`, `BlockEntityPrepTable.cs`, `BlockEntityPrepTableSlot.cs`, `BlockEntitySaltPan.cs` |
| `CollectibleBehavior/` | `BehaviorClamShuck.cs`, `BehaviorCoconutCrack.cs`, `BehaviorPlaceBurrito.cs`, `BehaviorShellCrush.cs` |
| `Entity/` | `EntityProjectileBarbed.cs` |
| `EntityBehavior/` | `EntityBehaviorExposure.cs`, `ExposureCondition.cs`, `ExposureConfig.cs` |
| `Item/` | `ItemMudRake.cs` |
| `Recipe/` | `GriddleRecipe.cs`, `GriddleRecipeRegistry.cs`, `PrepTableRecipe.cs`, `PrepTableRecipeRegistry.cs` |
| `Config/` | `DryingFrameConfig.cs`, `GriddleConfig.cs`, `SaltPanConfig.cs`, `MudRakeConfig.cs` |
| root | `SeafarerModSystem.cs` |

**Filenames are preserved verbatim.** `BlockDryingFrame.cs` stays `BlockDryingFrame.cs` — matches VSSurvivalMod's verbose-class-name-as-filename convention. This redundancy (class names + folder prefixes) is the cost of a flat namespace.

**Colocated helpers.** `ExposureCondition` (enum) and `ExposureConfig` (POCO) live next to `EntityBehaviorExposure` because they're only used by it — moving them to generic `Config/` or an `Enums/` folder would scatter tightly-coupled code. `*Config.cs` files for the four station blocks (DryingFrame, Griddle, SaltPan, MudRake) go in the shared `Config/` folder because the mod system loads them all at startup as one batch.

## Rename manifest

| Old | New |
|---|---|
| `SaltAndSand.csproj` | `Seafarer.csproj` |
| `SaltAndSandModSystem.cs` | `SeafarerModSystem.cs` |
| class `SaltAndSandModSystem` | class `SeafarerModSystem` |
| `namespace SaltAndSand` | `namespace Seafarer` |
| `Seafarer.sln` / `.slnx` | already exist in target repo — update project reference to new csproj |
| `<OutputPath>bin\$(Configuration)\Mods\mod</OutputPath>` | unchanged |
| `<AssemblyName>` (if present) | `Seafarer` |
| Harmony id `"seafarer"` | unchanged — content-level identifier, modid-neutral |
| `SaltAndSandConfig.json` (const in `SeafarerModSystem`) | unchanged — filename is an on-disk config; rename would churn existing user configs |
| modinfo.json `modid` | unchanged — `"seafarer"` |
| modinfo.json `name` | unchanged — `"Seafarer"` |

## Asset cleanup

One concrete change:

**Merge `itemtypes/resources/` into `itemtypes/resource/`.** `resources/` (plural) contains exactly one file, `seeds.json`. Every other resource item lives in singular `resource/`. Fix:

1. `git mv itemtypes/resources/seeds.json itemtypes/resource/seeds.json`
2. Grep the whole repo for `seafarer:resources/` / `itemtypes/resources/` references (likely in: recipes, patches, handbook). Update each to the new singular path.
3. Remove the now-empty `resources/` folder.

Everything else already matches VSSurvivalMod's asset conventions:
- `blocktypes/` with category subfolders ✓
- `itemtypes/` with category subfolders ✓
- `config/dialogue/`, `config/handbook/`, `config/quests/`, `config/training/`, `config/tradelists/` — all follow survival's `config/` pattern ✓
- `entities/`, `recipes/`, `shapes/`, `textures/`, `sounds/`, `patches/`, `lang/` — standard placements ✓

Seafarer correctly omits `music/`, `shaders/`, `worldgen/`, `worldproperties/` — those would be empty.

The ProgressionFramework scanner expects `config/quests/`, `config/training/`, `config/tradelists/` at exactly these paths; do not move them.

## External dependencies

- **ProgressionFramework** (`vsmod-progression-framework`, modid `progressionframework`) — Seafarer's `modinfo.json` keeps its existing hard dependency; `Seafarer.csproj` keeps its `ProjectReference` to the framework csproj at `/mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/ProgressionFramework.csproj`. No framework-side changes needed — the framework references Seafarer only by content identifiers (`seafarer:` asset paths, `EntityEvolvingTrader` class name) which are unchanged.

## Migration sequence

Each step builds cleanly before moving on:

1. Copy source tree: `vsmod-salt-and-sand/SaltAndSand/SaltAndSand/*` → `vsmod-seafarer/Seafarer/Seafarer/`. Exclude `bin/`, `obj/`, `.vs/`.
2. Copy `SaltAndSand/CLAUDE.md` → `Seafarer/CLAUDE.md` (at solution root).
3. Copy Python validators (`validate-assets.py`, `validate-food.py`, `vs_validators/`, `requirements-dev.txt`) to the new repo root.
4. Rename project files: `SaltAndSand.csproj` → `Seafarer.csproj`; `SaltAndSandModSystem.cs` → `SeafarerModSystem.cs`.
5. Rebase namespace: `sed -i 's|\bSaltAndSand\b|Seafarer|g'` on all `.cs` files. Includes both `namespace SaltAndSand` and qualified references like `SaltAndSand.Training` (though Training has already been extracted — should be 0 qualified references). Verify with a post-grep.
6. Reorganize `.cs` files into type folders via `git mv` per §"C# file organization".
7. Dedupe `itemtypes/resource[s]` per §"Asset cleanup".
8. Update `Seafarer.csproj` `<ProjectReference>` to the framework csproj (same absolute path as salt-and-sand's current reference).
9. Update the pre-existing `Seafarer.sln` / `.slnx` to reference the new `Seafarer/Seafarer.csproj` (they currently have template contents pointing at something else or nothing).
10. Update `CLAUDE.md` inside the new repo: replace references to "SaltAndSand" / "Salt and Sand" with "Seafarer"; add a note that this is the successor to `vsmod-salt-and-sand`.
11. Update framework's `CLAUDE.md` (`vsmod-progression-framework/ProgressionFramework/CLAUDE.md`): change the consumer reference from `vsmod-salt-and-sand` path to `vsmod-seafarer`.
12. Build `Seafarer.csproj` → 0 errors.
13. Smoke test: launch VS with framework + seafarer; confirm load, training ledger, trader spawn, quest accept, crafting XP — same matrix as the earlier extraction smoke test.

## Validation

- `dotnet build Seafarer/Seafarer/Seafarer.csproj` — must be 0 errors at every meaningful step.
- `python3 validate-assets.py` — run after asset dedupe.
- `python3 validate-food.py` — run after asset dedupe (food itemtypes live inside the moved `itemtypes/` tree).
- Manual smoke test at the end.

## Old repo disposition

`vsmod-salt-and-sand/` stays on disk. Add a one-line note at the top of its README (or CLAUDE.md) pointing to `vsmod-seafarer` as the successor. Do not delete.

## Out of scope

- Deleting or altering the old repo beyond a forwarding note.
- C# behavior changes, balance tweaks, new content.
- `modinfo.json` metadata changes other than implicit ones (none).
- Framework-side changes.
