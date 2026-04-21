# Gen Seafarer Structures — Design

## Motivation

Seafarer's three story-structure entries (Tortuga, Potato King's House, Wreck of the Crimson Rose) currently live in `assets/seafarer/worldgen/storystructures.json`. Base-game `GenStoryStructures` scans `worldgen/storystructures.json` across all mod assets, so it picks up Seafarer's entries and tries to place them with its own logic. That logic breaks for our use cases:

- **Tortuga (Coastal).** Base-game `EnumStructurePlacement.Surface` resolves Y to `seaLevel + OffsetY`. On a `shallowislands` landform the island terrain generates above that Y, so the structure's empty rooms get filled with dirt and stone. The only workaround inside the base-game system is filling every negative-space voxel with air meta-blocks in the schematic, which is impractical at Tortuga's scale.
- **Crimson Rose (Underwater).** `EnumStructurePlacement.Underwater` exists as an enum value but the base-game story-gen Y-resolution path only branches on `Surface`/`SurfaceRuin`/`Underground`. Underwater falls through to the hardcoded `locationHeight = 1` — the wreck ends up at bedrock, not the seabed.
- **Scattered wrecks (non-story).** Seafarer also has `oceanstructures.json` with chance-per-chunk scattered wrecks. The current `GenOceanStructures.cs` handles these via a lazy-reservation system that has accumulated complexity. Rather than maintain two parallel systems, we fold story + scattered into one.
- Base game provides `island` and `shallowislands` landforms but no ocean-aware validation — OceanMap sampling is the right primitive for ocean/coastal/seabed checks and it isn't wired in for story structures.

## Goal

Replace `GenOceanStructures.cs` with a single class `GenSeafarerStructures` that:

1. Duplicates the working parts of base-game `GenStoryStructures` (init-time determination, savegame persistence, per-chunk `PlacePartial`, land-claim emission, block-patch suppression, skip-generation-categories).
2. Adds ocean-aware placement modes: `Coastal`, `Underwater`, `OceanSurface`.
3. Handles both story (deterministic, unique) and scattered (chance-per-chunk) structures via a single `storyStructure` flag on each config entry.
4. Resolves the Tortuga island-merge problem via opt-in footprint clearing.
5. Leaves hooks for future decorators (e.g. coral growth) without shipping the decorators themselves.
6. Emits `GeneratedStructure` records with a stable `Group` name and exposes story-location lookups so a future ocean-map item can resolve codes to coordinates.

## Non-goals

- No decorator implementations (coral, etc.) in this build — only hook surface.
- No changes to the base-game story-structure schema. Seafarer maintains its own schema and does not try to extend `WorldGenStoryStructure`.
- No ocean-map item in this build. We only ensure the records are queryable.
- No migration of already-generated worlds. Any world that had the old system will have orphaned reservations in its savegame; those remain unused and harmless.

## Architecture

### Files

- **New**: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs` — the single mod system.
- **New**: `Seafarer/Seafarer/assets/seafarer/worldgen/seafarerstructures.json` — merged config replacing `storystructures.json` + `oceanstructures.json`.
- **Delete**: `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json` — stops base-game pickup.
- **Delete**: `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json` — folded into the new file.
- **Delete**: old `GenOceanStructures.cs`.

### Schema (`seafarerstructures.json`)

```json
{
  "schematicYOffsets": { "story/seafarer:tortuga": -27 },
  "rocktypeRemapGroups": {},
  "structures": [
    {
      "code": "tortuga",
      "group": "seafarerstructure",
      "name": "Tortuga",
      "schematics": ["costal/tortuga"],
      "placement": "Coastal",
      "storyStructure": true,
      "clearFootprint": true,
      "buildProtected": true,
      "protectionLevel": 10,
      "buildProtectionName": "custommessage-tortuga",
      "buildProtectionDesc": "Tortuga — neutral port",
      "allowUseEveryone": true,
      "allowTraverseEveryone": true,
      "requireCoast": true,
      "minSpawnDistX": -3000, "maxSpawnDistX": 3000,
      "minSpawnDistZ": -3000, "maxSpawnDistZ": 3000,
      "landformRadius": 10,
      "generateGrass": true,
      "disableSurfaceTerrainBlending": true,
      "skipGenerationCategories": {
        "structures": 300, "trees": 200, "shrubs": 150,
        "hotsprings": 250, "patches": 100, "pond": 200, "rivulets": 100
      }
    },
    {
      "code": "wreck-crimsonrose",
      "group": "seafarerstructure",
      "schematics": ["underwater/wreck-crimson-rose"],
      "placement": "Underwater",
      "storyStructure": true,
      "requireOcean": true,
      "minWaterDepth": 10, "maxWaterDepth": 80,
      "minSpawnDistX": -3000, "maxSpawnDistX": 3000,
      "minSpawnDistZ": -3000, "maxSpawnDistZ": 3000,
      "landformRadius": 0,
      "skipGenerationCategories": { "structures": 80 }
    },
    {
      "code": "wreck-one",
      "group": "seafarerstructure",
      "schematics": ["underwater/wreck-one"],
      "placement": "Underwater",
      "storyStructure": false,
      "chance": 0.015,
      "requireOcean": true,
      "minWaterDepth": 10, "maxWaterDepth": 50,
      "suppressTrees": true,
      "randomRotation": true
    }
  ]
}
```

Fields (new or Seafarer-specific):

- `placement` — `Surface` | `SurfaceRuin` | `Coastal` | `Underwater` | `OceanSurface`.
- `storyStructure` (default `false`) — `true` = deterministic init-time placement; `false` = chance-per-chunk roll.
- `chance` — only consulted when `storyStructure: false`. Float in `[0, 1]`.
- `clearFootprint` (default `false`) — if `true`, the chunk-slice placement code sets every block in the structure's XYZ bounding box to air (solid layer) before `PlacePartial`. Fixes Tortuga merge without schematic edits.
- `requireOcean` / `requireCoast` (default `false`) — coarse OceanMap gates checked at determination / chunk-roll.
- `minWaterDepth` / `maxWaterDepth` (defaults 0 / 255) — sea-level-minus-terrain-height bounds. Enforced on Underwater/OceanSurface.
- `postPlaceDecorators` (default `[]`) — list of decorator codes passed through to the `OnStructurePlaced` event payload. No decorators ship in this build.
- `randomRotation` (default `true`) — if true, picks 0/90/180/270 per placement.
- `suppressTrees` (default `false`) — sets `SuppressTreesAndShrubs` on the emitted `GeneratedStructure`.

Inherited from base-game `WorldGenStoryStructure` and kept verbatim:

- `group`, `name`, `schematics`, `dependsOnStructure`
- `minSpawnDistX/Z`, `maxSpawnDistX/Z`, `landformRadius`, `generationRadius`
- `skipGenerationCategories`, `forceRain`, `forceTemperature`, `requireLandform`
- `useWorldgenHeight`, `disableSurfaceTerrainBlending`, `generateGrass`
- `buildProtected`, `protectionLevel`, `buildProtectionName`, `buildProtectionDesc`
- `allowUseEveryone`, `allowTraverseEveryone`, `excludeSchematicSizeProtect`
- `extraLandClaimX/Z`, `customLandClaims`

### Class outline

```csharp
namespace Seafarer.WorldGen
{
    public enum EnumSeafarerPlacement
    {
        Surface, SurfaceRuin, Coastal, Underwater, OceanSurface
    }

    public class SeafarerStructure { /* fields above, mirrors WorldGenStoryStructure where applicable */ }
    public class SeafarerStructuresConfig { public SeafarerStructure[] Structures; /* + yOffsets, rocktypeRemaps */ }

    public class SeafarerStructureLocation : IStructureLocation
    {
        public string Code;
        public BlockPos CenterPos;
        public Cuboidi Location;
        public int LandformRadius;
        public int GenerationRadius;
        public int DirX;
        public Dictionary<int,int> SkipGenerationFlags;
        public int MaxSkipGenerationRadiusSq;
        public bool DidGenerate;
        public int WorldgenHeight = -1;
        public string RockBlockCode;
    }

    public class SeafarerStructurePlacedEvent
    {
        public SeafarerStructure Def;
        public string Code;
        public int ChunkX, ChunkZ;
        public Cuboidi Bounds;
        public int BlocksPlacedInSlice;
    }

    public class GenSeafarerStructures : ModStdWorldGen, IBlockPatchModifier
    {
        public SeafarerStructuresConfig scfg;
        public IReadOnlyDictionary<string, SeafarerStructureLocation> Locations => storyLocations;
        public event Action<SeafarerStructurePlacedEvent> OnStructurePlaced;

        // core hooks
        public override void StartServerSide(ICoreServerAPI api) { /* Vegetation pass, save/load, commands */ }
        private void InitWorldGen() { LoadConfig(); DetermineStoryStructures(); }
        private void OnChunkColumnGen(IChunkColumnGenerateRequest req) { /* story slice + scattered roll */ }

        // placement helpers
        private int ResolveY(SeafarerStructure def, int terrainHeight, BlockSchematic schem);
        private bool ValidateOceanPlacement(SeafarerStructure def, IMapRegion region, int posX, int posZ, int terrainHeight);
        private void ClearFootprint(IServerChunk[] chunks, Cuboidi bounds, int chunkX, int chunkZ);
        private void EmitGeneratedStructure(IMapRegion region, SeafarerStructure def, Cuboidi bounds);
        private void EmitLandClaims(SeafarerStructure def, Cuboidi bounds);

        // command handlers (tp, setpos, listmissing, place, list)
    }
}
```

### Execution order

- `ExecuteOrder = 0.21` — just after base-game `GenStoryStructures` (0.2), before `GenStructures` (0.3). Ensures story locations are determined before scattered-structure chunk rolls.
- Chunk-gen hook: `EnumWorldGenPass.Vegetation` — heightmap is final, trees not yet placed.

### Placement Y resolution

Implemented in `ResolveY`:

| Placement     | Y                                                            |
|---------------|--------------------------------------------------------------|
| Surface       | `seaLevel + OffsetY` (same as base game)                     |
| SurfaceRuin   | `terrainHeight - SizeY + OffsetY` (same as base game)        |
| Coastal       | `terrainHeight + OffsetY` — sits on landform, not sea level  |
| Underwater    | `terrainHeight + OffsetY` — sits on seabed                   |
| OceanSurface  | `seaLevel + OffsetY` — floats on water                       |

For story structures the per-chunk handler reads `terrainHeight` from `request.Chunks[0].MapChunk.WorldGenTerrainHeightMap` at the origin corner on first arrival and caches the resolved Y on `SeafarerStructureLocation.WorldgenHeight`, matching base-game behavior.

### Ocean validation

`ValidateOceanPlacement`:

- Samples OceanMap at 9 points across the structure's XZ footprint (center + 4 corners + 4 edge midpoints), bilinear-interpolated from region data.
- `requireOcean`: center must be ocean, ≥7 of 9 samples ocean.
- `requireCoast`: center must have `beach > 0` OR (`ocean > 0` AND `waterDepth <= maxWaterDepth`).
- Depth band: applies when `placement` is `Underwater` or `OceanSurface`, or when any of `minWaterDepth`/`maxWaterDepth` are non-default.
- Story structures run this check during `DetermineStoryStructures` (up to 30 candidate positions per structure). Scattered structures run it at chunk-roll time.

### Footprint clearing (Tortuga fix)

When `clearFootprint: true`, for each chunk slice the structure touches:

1. Compute the intersection of the structure's bounding box with the current chunk.
2. Walk every block position in that intersection.
3. Call `chunks[y / chunksize].SetBlockUnsafe(index, 0)` on the solid layer (or use `worldgenBlockAccessor.SetBlock` with `BlockLayersAccess.Solid`) with the air block id.

This runs **before** `PlacePartial`, so the schematic's solid blocks then overwrite the cleared region, and any interior negative-space voxels stay as air instead of being filled by subsequent passes. `disableSurfaceTerrainBlending: true` on the same entry prevents the blend pass from re-flooding the edges.

Opt-in only — off by default so scattered wrecks and other structures that want to mix with terrain still can.

### Story-structure determination

Mirrors base-game `DetermineStoryStructures` closely:

- Seeded LCG (`seed ^ code.GetHashCode()`) for per-structure determinism.
- `dependsOnStructure` with `"spawn"` resolves to saved spawn point or map center (fallback).
- For each structure: pick direction signs, draw `distanceX/Z` within min/max spawn-dist box, validate ocean constraints if any, retry up to 30 attempts on failure.
- Store result in `storyLocations` dict and `attemptedToGenerateSeafarerLocation` savegame list.
- Missing-structure reporting via the existing `listmissing` pattern.

### Per-chunk handling

`OnChunkColumnGen` runs two passes:

1. **Story slice.** For each `SeafarerStructureLocation` whose `Location` cuboid intersects this chunk, resolve Y if needed, optionally `ClearFootprint`, then `PlacePartial`. On first placement (tracked by `DidGenerate`), emit `GeneratedStructure`, emit land claims, fire `OnStructurePlaced`.
2. **Scattered roll.** For each non-story `SeafarerStructure` entry, seed RNG from `(chunkX, chunkZ, code)`, roll chance. On hit, pick a random local X/Z, validate ocean constraints, resolve Y, optionally clear footprint, `PlacePartial`. Emit records and fire the hook.

Both passes use `BlockSchematicPartial.PlacePartial` so the schematic writes only to the currently-generating chunk's cells.

### Extension hooks (for future coral decorator)

```csharp
public event Action<SeafarerStructurePlacedEvent> OnStructurePlaced;
```

Fires once per chunk slice that actually placed blocks (`blocksPlaced > 0`). Payload includes the structure's full def (so the decorator can read `postPlaceDecorators`), the placed bounds clipped to this chunk, and the chunk coordinates. A decorator subscribes in its own `StartServerSide`, checks `evt.Def.PostPlaceDecorators.Contains("grow-coral")`, and seeds coral within the clipped bounds.

No decorator ships in this build. `postPlaceDecorators: []` on every entry.

### Land claims

Ported verbatim from base-game `GenStoryStructures.OnChunkColumnGen` land-claim block. Claims are emitted on first-chunk-slice placement (gated by `blocksPlaced > 0 && buildProtected`). Supports:

- Main structure cuboid (skippable via `excludeSchematicSizeProtect`).
- `extraLandClaimX/Z` — rectangular buffer around the structure.
- `customLandClaims` — author-defined cuboids, offset by structure origin.

Tortuga and Potato King's House get `buildProtected: true, protectionLevel: 10`, matching trader protection.

### Persistence

- `seafarer-structure-locations` — serialized `OrderedDictionary<string, SeafarerStructureLocation>`, written on `GameWorldSave` when dirty.
- `attemptedToGenerateSeafarerLocation` — list of codes we've run determination for (so `/wgen regen` doesn't re-determine).
- No reservation persistence (the current `seafarer-ocean-reservations` key is abandoned — harmless if present in old saves).

### Commands

```
/wgen seafarer tp <code>             # teleport to a story structure
/wgen seafarer setpos <code> <pos> [confirm]   # move a story structure
/wgen seafarer listmissing           # list story structures that failed to determine
/wgen seafarer rmsc <code>           # clear schematics-spawned counter (regen helper)
/wgen seafarer place <code>          # force-place a structure at player position (debug)
/wgen seafarer list                  # list all registered codes with validation status
```

Aliases: `/tpseafarerloc` for `tp`, `/setseafarerstrucpos` for `setpos`.

### Map-readiness

- Every placement emits `mapRegion.AddGeneratedStructure(new GeneratedStructure { Code = def.Code, Group = "seafarerstructure", Location = bounds.Clone(), SuppressTreesAndShrubs = def.SuppressTrees, SuppressRivulets = true })`.
- `Group = "seafarerstructure"` (not `"ocean"` — scoped to the mod, not placement type).
- Public `Locations` dict exposes story-structure positions by code without requiring map-region walks.
- A future ocean-map item crafts with `Code` embedded, looks up `GenSeafarerStructures.Locations[code].CenterPos` for story entries, or scans `GeneratedStructures.Where(s => s.Group == "seafarerstructure" && s.Code == code)` across regions for scattered entries.

## Build sequence

Handled by the plan doc, but for context, the expected order is:

1. Write `SeafarerStructure` / `SeafarerStructuresConfig` / `SeafarerStructureLocation` types.
2. Write `GenSeafarerStructures` scaffolding (StartServerSide, save/load, InitWorldGen, LoadConfig, DetermineStoryStructures).
3. Port `OnChunkColumnGen` story path from base game, adapted for `EnumSeafarerPlacement`.
4. Add `ValidateOceanPlacement` and wire into determination + scattered roll.
5. Add `ClearFootprint`.
6. Add scattered-roll path.
7. Add `OnStructurePlaced` hook.
8. Add commands.
9. Write new `seafarerstructures.json`; delete old `storystructures.json` and `oceanstructures.json`.
10. Delete old `GenOceanStructures.cs`.
11. Build, launch, verify in-game (Tortuga merges cleanly, Crimson Rose generates on seabed, scattered wrecks still roll).

## Risks and notes

- **Hash category collisions.** `SkipGenerationCategories` uses SHA256-derived ints matched against base-game category hashes. These need to match the base game's category names exactly (`"trees"`, `"shrubs"`, `"structures"`, `"hotsprings"`, `"patches"`, `"pond"`, `"rivulets"`) — verified by reading base-game source.
- **`ClearFootprint` cost.** For a large coastal structure (Tortuga is ~80×80 at landform radius) the clear is up to ~80 × 80 × SizeY block writes per chunk slice. Scoped to the schematic's actual bounds and only runs once per slice; should be fine but worth watching in profiling.
- **Determination-time ocean validation without loaded map regions.** Fresh worlds at `InitWorldGen` have no map regions loaded, so OceanMap sampling on a bare candidate position returns 0 everywhere. Mitigation is automatic (no config knob): determination picks X/Z based on spawn-dist bounds only, skipping ocean validation. The per-chunk story handler then runs ocean validation the first time it resolves Y for the origin chunk and drops the reservation if the fine-grained check fails (matching the approach in the old `GenOceanStructures.PlaceOceanSurfaceSlice`). If validation fails, the structure is reported via `listmissing` so the admin can relocate it manually with `setpos`.
- **Old savegame data.** Worlds created with the previous `GenOceanStructures` will have `seafarer-ocean-reservations` and `seafarer-ocean-structure-counts` entries. Both are ignored by the new system and do no harm. Not cleaning them up keeps the first build simple.
- **Base-game `storystructures.json` deletion.** Once Seafarer's file is gone, base-game `GenStoryStructures` has nothing to pick up from this mod. Verified: `api.Assets.GetMany` returns empty for a missing asset, no error.
