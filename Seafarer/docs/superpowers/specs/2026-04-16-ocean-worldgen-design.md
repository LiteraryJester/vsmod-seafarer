# Ocean Structure Worldgen System

## Overview

A config-driven worldgen system for the Seafarer mod that places structures in ocean, coastal, and buried-underwater locations. Fills a gap in Vintage Story's base game, which has no native support for underwater or coastal structure placement.

The first structure is the Crimson Rose shipwreck (Celeste's quest), with the system designed for future ocean content (coral ruins, sunken cargo, beached wrecks, etc.).

## Architecture

### Approach

Standalone `ModSystem` in the Seafarer mod. No Harmony patches, no modifications to base game files, no dependency on VintagestoryLib internals. The system hooks into VS's worldgen pipeline via public API events and uses `BlockSchematic.Place()` with `IWorldGenBlockAccessor` for placement.

### New Files

| File | Purpose |
|------|---------|
| `Seafarer/WorldGen/GenOceanStructures.cs` | ModSystem — config loading, worldgen hook, placement logic, debug command |
| `assets/seafarer/worldgen/oceanstructures.json` | Structure definitions |

### Modified Files

| File | Change |
|------|--------|
| `assets/seafarer/itemtypes/lore/map-crimsonrose.json` | Change `schematiccode` from `"buriedtreasurechest"` to `"wreck-crimsonrose"` |

## Config Format

Located at `assets/seafarer/worldgen/oceanstructures.json`:

```json5
{
    structures: [
        {
            "code": "wreck-crimsonrose",
            "schematics": ["underwater/wreak-crimson-rose"],
            "placement": "underwater",
            "chance": 0.015,
            "minWaterDepth": 4,
            "maxWaterDepth": 50,
            "offsetY": 0,
            "maxCount": 5,
            "suppressTrees": true,
            "randomRotation": true
        }
    ]
}
```

### Field Definitions

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `code` | string | yes | — | Unique identifier. Must match `schematiccode` in any locator map item that points to this structure. |
| `schematics` | string[] | yes | — | Paths relative to `worldgen/schematics/`. One chosen at random per placement. |
| `placement` | string | yes | — | `"underwater"`, `"coastal"`, or `"buried-underwater"`. |
| `chance` | float | yes | — | Probability (0.0–1.0) of attempting placement per chunk. Unlike the base game's `structures.json` (which uses an opaque internal scale), this is a straightforward per-chunk probability: 0.01 = 1% chance per chunk, 0.1 = 10%. |
| `minWaterDepth` | int | no | 0 | Minimum water depth (sea level minus terrain height) for valid placement. |
| `maxWaterDepth` | int | no | 255 | Maximum water depth. |
| `offsetY` | int | no | 0 | Vertical offset from calculated placement Y. |
| `maxCount` | int | no | unlimited | Global cap on total instances. When omitted, no limit. |
| `suppressTrees` | bool | no | false | Whether to set `SuppressTreesAndShrubs` on the registered `GeneratedStructure`. |
| `randomRotation` | bool | no | true | Apply random 0/90/180/270 degree rotation. |

## Placement Modes

### `underwater`

Structure placed on the ocean floor, visible to players swimming/diving.

**Constraints:** `OceanMap > 0` at the candidate position, water depth in `[minWaterDepth, maxWaterDepth]`.

**Y position:** `terrainHeight + offsetY` (schematic bottom sits on the seabed).

**Use cases:** Shipwrecks, sunken ruins, underwater treasure sites.

### `coastal`

Structure placed near the shoreline — partially submerged, on the beach, or just above waterline.

**Constraints:** `BeachMap > 0` at the candidate position, OR water depth in `[0, maxWaterDepth]` with `OceanMap > 0`. This catches both sandy beaches and rocky shorelines where BeachMap might be zero.

**Y position:** `terrainHeight + offsetY`.

**Use cases:** Beached wrecks, tidal pools, coastal camps, driftwood piles.

### `buried-underwater`

Structure placed below the ocean floor. Player must dig to find it.

**Constraints:** Same ocean detection as `underwater` — `OceanMap > 0`, water depth in `[minWaterDepth, maxWaterDepth]`.

**Y position:** `terrainHeight - schematicHeight + offsetY` (schematic top is at or near the seabed surface).

**Use cases:** Buried treasure beneath the ocean floor, deeply sunken wrecks encased in sediment.

## GenOceanStructures ModSystem

### Lifecycle

```
StartServerSide()
├── Register InitWorldGenerator handler ("standard")
├── Register ChunkColumnGeneration handler (TerrainFeatures pass)
├── Register GetWorldgenBlockAccessor handler
└── Register /ocean debug command

InitWorldGen() — fired once at world load
├── Load oceanstructures.json via api.Assets.Get()
├── For each structure definition:
│   ├── Load each schematic via api.Assets.TryGet()
│   ├── Call schematic.Init(blockAccessor)
│   ├── Create 4 rotated copies via TransformWhilePacked(0, 90, 180, 270)
│   └── Cache in dictionary keyed by structure code
└── Initialize LCGRandom with world seed

OnChunkColumnGen(request) — fired per chunk at TerrainFeatures pass
├── For each structure definition:
│   ├── Seed RNG with chunkX/chunkZ
│   ├── Roll chance — skip on miss
│   ├── Check maxCount — count matching GeneratedStructures in region, skip if cap hit
│   ├── Pick random XZ within chunk
│   ├── Read OceanMap / BeachMap for candidate position
│   ├── Read terrain height from WorldGenTerrainHeightMap
│   ├── Calculate waterDepth = seaLevel - terrainHeight
│   ├── Validate placement mode constraints
│   ├── Calculate Y position per placement mode
│   ├── Pick random schematic variant
│   ├── Pick random rotation (if randomRotation = true)
│   ├── Place via schematic.Place(worldgenBlockAccessor, world, startPos)
│   └── Register GeneratedStructure on the MapRegion
└── (next chunk)
```

### ExecuteOrder

`0.31` — after the base game's GenStructures (0.3), ensuring terrain is fully formed.

### Schematic Caching

All schematics are loaded and rotated at `InitWorldGen` time. For each schematic, 4 rotations (0, 90, 180, 270) are pre-computed via `BlockSchematic.TransformWhilePacked()` and stored in an array. At placement time, a random rotation is selected by index.

### Multi-Chunk Schematics

`BlockSchematic.Place()` writes blocks via `IWorldGenBlockAccessor.SetBlock()`, which handles cross-chunk writes to any loaded chunk in the generation batch. Blocks that fall outside loaded chunks are silently dropped — same behavior as the base game's structure placement used by BetterRuins (which places schematics up to 100x106 blocks via the same mechanism).

No `PlacePartial`, origin constraints, or deferred placement tracking needed.

### Global Count Tracking

When `maxCount` is set, the system counts existing instances of that structure code in the current region's `GeneratedStructures` list and its immediate neighbors (loaded regions only). This is an approximation — distant regions aren't checked — but provides sufficient density control for rare structures. Same pattern the base game uses for structure spacing.

### Structure Registration

After successful placement, the structure is registered for locator map discovery:

```csharp
region.AddGeneratedStructure(new GeneratedStructure()
{
    Code = structureDef.Code,
    Group = "ocean",
    Location = new Cuboidi(
        startPos.X, startPos.Y, startPos.Z,
        startPos.X + schematic.SizeX - 1,
        startPos.Y + schematic.SizeY - 1,
        startPos.Z + schematic.SizeZ - 1
    ),
    SuppressTreesAndShrubs = structureDef.SuppressTrees,
    SuppressRivulets = true
});
```

This makes the structure discoverable by `ModSystemStructureLocator.FindFreshStructureLocation()`, which is called by `ItemLocatorMap` when a player uses a map item.

## Locator Map Integration

The existing `map-crimsonrose.json` item uses `class: "ItemLocatorMap"` with a `schematiccode` property. The only change needed:

```json5
"locatorPropsbyType": {
    "*": {
        "schematiccode": "wreck-crimsonrose",
        "waypointtext": "location-crimsonrose",
        "waypointicon": "x",
        "waypointcolor": [0.8, 0.1, 0.1, 1],
        "randomX": 15,
        "randomZ": 15
    }
}
```

`randomX`/`randomZ` add imprecision so the player searches the area rather than walking to the exact block.

**Runtime chain:**
1. World generates → `GenOceanStructures` places wreck, registers as `"wreck-crimsonrose"`
2. Celeste gives map → dialog `giveitemstack` gives `seafarer:map-crimsonrose`
3. Player uses map → `ItemLocatorMap` calls `FindFreshStructureLocation("wreck-crimsonrose", ...)` within 350-chunk radius
4. Waypoint appears on world map at approximate location
5. Player dives to site, finds wreck with sealed chest

## Debug Command

Server command `/ocean` with subcommands:

- `/ocean place <code>` — force-place a structure at the player's current position. Useful for testing schematics, transforms, and loot without waiting for worldgen.
- `/ocean list` — list all registered ocean structure codes and their config.

Requires admin privilege.

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Schematic file not found at load time | Log warning, skip that structure definition. Other structures still generate. |
| `OceanMap` or `BeachMap` is null for a region | Skip all ocean structure generation for that region. Some world types may not generate these maps. |
| Config file not found | Log info message, system becomes a no-op. Mod still loads normally. |
| `maxCount` reached | Structure silently skipped for that chunk. No log spam. |
| Schematic extends beyond loaded chunks | `SetBlock` calls for unloaded positions are silently dropped by `IWorldGenBlockAccessor`. Acceptable visual tradeoff (a few missing edge blocks). |

## Future Extensibility

The config-driven design supports adding new ocean content without C# changes:

- Add new schematic files under `worldgen/schematics/underwater/` or `worldgen/schematics/coastal/`
- Add entries to `oceanstructures.json`
- Optionally add new locator map items pointing to the structure code

Potential future placement modes (would require C# changes):
- `deep-ocean` — only in areas with water depth > N, for abyssal content
- `river` — along rivers, if river detection maps become available
