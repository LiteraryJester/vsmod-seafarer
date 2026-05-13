# Seafloor Fill for OceanSurface Structures — Design

**Date:** 2026-05-09
**Status:** Approved

## Summary

Add a post-placement seafloor fill pass to `GenSeafarerStructures` that closes
the visible gap between an ocean structure's underside and the seafloor. Driven
by a new optional JSON property `seafloorFillBlock` on `SeafarerStructure`.
When set, after `PlacePartial` runs for a chunk slice, the new pass walks the
slice's portion of the structure footprint and — for every column that the
schematic actually occupies — raises the seafloor up to one block below the
structure's lowest Y using the configured fill block.

Tortuga is the immediate consumer (`"seafloorFillBlock": "game:muddygravel"`),
but the property is general so future ocean structures can opt in with their
own block choice.

## Motivation

Tortuga is `OceanSurface` placement with `yOffset: -12`, so its bottom is
anchored at `SeaLevel - 12`. The forced `saltwater` landform produces a
roughly consistent ocean depth, but landform noise still leaves the actual
seafloor several blocks below Tortuga's underside in many places. Result:
visible gaps of open water under the island where the seafloor falls away.
Worse with mods like LandformOverhaul that shift terrain Y.

The schematic itself can't be made deeper without making Tortuga visibly
poke up out of shallower spots. We need terrain to come up to meet Tortuga,
not the other way around.

## Design

### 1. New JSON property

Add to `SeafarerStructure`:

```csharp
[JsonProperty]
public string SeafloorFillBlock;
```

When null/empty, the new pass is a no-op for that structure. When set to a
valid asset code (e.g. `game:muddygravel`), the pass runs after `PlacePartial`
for every chunk slice the structure occupies.

Resolved to a block ID at load time (in `LoadConfig`, alongside the other
asset-resolution work) and cached on the `SeafarerStructure` def as an
internal `int seafloorFillBlockId`. Resolution failure logs an error and
leaves the ID at `0`, which the runtime check treats as "no fill".

### 2. Schematic column-occupancy mask

Precompute once per structure at load time:

```csharp
internal bool[,] schematicColumnMask;  // [sizeX, sizeZ] — true if any
                                       // non-air, non-fluid block in column
internal int schematicLowestY;          // lowest local Y with a solid block
```

Built by walking `def.schematicData.Indices` once: for each packed index,
decode using the standard `BlockSchematic` bit layout —
`lx = idx & 0x3ff`, `lz = (idx >> 10) & 0x3ff`, `ly = (idx >> 20) & 0x3ff`.
Look up the block via `BlockCodes[BlockIds[i]]` and `api.World.GetBlock(...)`,
then set `mask[lx, lz] = true` if the block is solid — i.e. not air
(`block.Id != 0`) and not `block.ForFluidsLayer`. Track the minimum `ly`
seen across all solid entries as `schematicLowestY`.

This walk is small (Tortuga is ~180×30×170, mostly empty), and the result
is cached for the lifetime of the worldgen system — no per-chunk recompute.

The mask is stored on the `SeafarerStructure` def (not the schematic) so
re-loading config doesn't lose it, and so it lives next to the other
internal worldgen state (`replacewithblocklayersBlockids`,
`resolvedRockTypeRemaps`).

### 3. Fill pass

New method:

```csharp
private void FillSeafloorBelow(
    SeafarerStructure def,
    SeafarerStructureLocation loc,
    IChunkColumnGenerateRequest request,
    int chunkX,
    int chunkZ);
```

Called from `PlaceStorySlices` immediately after `PlacePartial` returns a
positive `blocksPlaced`, when `def.seafloorFillBlockId > 0`. (Scattered
rolls aren't a use case yet; only story-slice path needs it for Tortuga.
Easy to add to `PlaceScatteredRolls` later if needed.)

Algorithm:

1. Compute `bottomY = loc.Location.Y1 + def.schematicLowestY`. (`Y1` is the
   structure's resolved world-Y origin; `schematicLowestY` is local-Y of the
   lowest solid block.)
2. Compute the XZ intersection of the structure's footprint with the
   currently-generating chunk:
   ```
   clipMinX = max(loc.Location.X1, chunkX * 32)
   clipMaxX = min(loc.Location.X2, chunkX * 32 + 32)
   clipMinZ = max(loc.Location.Z1, chunkZ * 32)
   clipMaxZ = min(loc.Location.Z2, chunkZ * 32 + 32)
   ```
   Skip if empty.
3. For each `(worldX, worldZ)` in the clipped range:
   - Translate to schematic-local: `localSchemX = worldX - loc.Location.X1`,
     `localSchemZ = worldZ - loc.Location.Z1`.
   - If `def.schematicColumnMask[localSchemX, localSchemZ]` is false, skip
     this column (open water beyond Tortuga's silhouette).
   - Read the column's terrain Y from
     `chunks[0].MapChunk.WorldGenTerrainHeightMap[localChunkZ * 32 + localChunkX]`,
     where `localChunkX = worldX - chunkX * 32` etc.
   - If `terrainY >= bottomY - 1`, skip (no gap to fill).
   - Walk Y from `terrainY + 1` up through `bottomY - 1`. For each Y:
     - Compute the chunk-local block index: `(localY * 32 + localChunkZ) * 32 + localChunkX`.
     - `chunk.Data.SetBlockUnsafe(blockIndex, def.seafloorFillBlockId)`.
     - `chunk.Data.SetFluid(blockIndex, 0)` (clears any water in that cell).
4. After the loop, call `UpdateHeightmap(request, worldgenBlockAccessor)`
   so the chunk's terrain/rain heightmaps reflect the new seafloor. The
   existing `OceanSurface` branch deliberately skips this update for the
   schematic placement (the structure sits on water and doesn't change
   the terrain heightmap), but the fill *does* change terrain — water
   below sea level is replaced with a solid block — so the heightmap must
   be rebuilt regardless of placement type.

Block writes use the same `IServerChunk[]` pattern as `ClearFootprint`,
which already handles cross-chunk-Y indexing safely.

### 4. Wiring

`LoadConfig` (after schematic load):

```csharp
if (!string.IsNullOrEmpty(def.SeafloorFillBlock))
{
    var block = api.World.GetBlock(new AssetLocation(def.SeafloorFillBlock));
    if (block == null)
    {
        api.Logger.Error(
            "Seafarer structure '{0}': seafloorFillBlock '{1}' not found.",
            def.Code, def.SeafloorFillBlock);
    }
    else
    {
        def.seafloorFillBlockId = block.Id;
    }

    BuildSchematicColumnMask(def);  // populates def.schematicColumnMask, schematicLowestY
}
```

`PlaceStorySlices` after the existing `if (blocksPlaced <= 0) continue;`
guard, before the `Surface*` heightmap update block:

```csharp
if (def.seafloorFillBlockId > 0 && def.schematicColumnMask != null)
{
    FillSeafloorBelow(def, loc, request, chunkX, chunkZ);
}
```

### 5. Tortuga config

In `assets/seafarer/worldgen/seafarerstructures.json`, on the `tortuga`
structure entry:

```json
"seafloorFillBlock": "game:muddygravel"
```

No other Tortuga config changes needed.

## Edge cases

- **Terrain already at/above structure bottom:** column is skipped; no
  overhanging muddy gravel above existing terrain.
- **Schematic column whose lowest local block is well above
  `schematicLowestY`** (e.g. an interior tower with no floor below it):
  still filled to `bottomY - 1`. Acceptable per the agreed "cover the lowest
  level" rule; in practice Tortuga's island base is contiguous so this
  shouldn't surface.
- **Block ID resolution failure:** logged at `Error` level; the ID stays 0
  and the runtime guard short-circuits the fill — structure still places,
  just without the fill.
- **`PlaceScatteredRolls`:** intentionally not wired. Add later when a
  scattered ocean structure needs it.
- **`disableSurfaceTerrainBlending`:** unrelated; acts on the schematic's
  edge blending. Fill runs regardless.

## Out of scope (deferred)

- Noise / variation in the fill top to make the join look more natural.
  For now the fill produces a flat plateau at `bottomY - 1`; we'll add
  per-column jitter as a follow-up once the basic fix is in.
- Multiple fill blocks per structure (e.g. sand near edges, gravel deep).
  Single block ID is enough for Tortuga.
- Blending the fill block with the surrounding seafloor block layers.
  Muddy gravel is the agreed visible target; let it read as a clear
  "Tortuga-shaped sea bed" for now.

## Files changed

- `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs`
  - Add `SeafloorFillBlock` JSON property + `seafloorFillBlockId`,
    `schematicColumnMask`, `schematicLowestY` internal fields on
    `SeafarerStructure`.
  - Add `BuildSchematicColumnMask` and `FillSeafloorBelow` methods.
  - Wire block-ID resolution + mask build in `LoadConfig`.
  - Wire `FillSeafloorBelow` call in `PlaceStorySlices`.
- `Seafarer/Seafarer/assets/seafarer/worldgen/seafarerstructures.json`
  - Add `"seafloorFillBlock": "game:muddygravel"` to the `tortuga` entry.

## Testing

- World-gen a fresh world, teleport to Tortuga (`/wgen seafarer tp tortuga`),
  inspect underside in spectator mode for gap closure.
- Reset Tortuga's generation state (`/wgen seafarer rmsc tortuga`), regenerate
  surrounding chunks, confirm new gen still fills correctly.
- Verify other story structures (`potatoking`, wrecks) without
  `seafloorFillBlock` are unchanged.
- Confirm no "set block outside generating chunks" warnings in server log
  during Tortuga generation.
