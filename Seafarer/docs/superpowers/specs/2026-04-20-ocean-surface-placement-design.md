# Ocean Surface Placement for GenOceanStructures — Design

**Date:** 2026-04-20
**Status:** Approved

## Summary

Add an `OceanSurface` placement mode to Seafarer's `GenOceanStructures`
worldgen system. Unlike the existing per-chunk "place everything at once"
path (which silently drops blocks outside the currently-generating chunk
and caps effective structure size at one chunk column), `OceanSurface`
adopts the chunk-iterative pattern base-game `GenStoryStructures` uses:
reserve a location at first opportunity, then paint the schematic piece-
by-piece over subsequent chunk-gen events via `BlockSchematicPartial.PlacePartial`.

The new mode places schematics at sea level (plus a configurable `OffsetY`)
on spots validated as open ocean — corners and center of the footprint must
be ocean, most edge midpoints too. This lets Tortuga and future large ocean
structures return to Seafarer's own worldgen system (where we control
placement semantics end-to-end) without the cross-chunk write failures of
the prior approach.

## Motivation

Two converging problems:

1. **Cross-chunk writes.** `ChunkColumnGeneration`'s `IWorldGenBlockAccessor`
   only writes to chunks currently mid-generation. Tortuga's 178×171 footprint
   spans many chunks, so the old "place it all in one pass" approach dropped
   most of the structure with *"Tried to set block outside generating chunks"*
   warnings.

2. **Story structures were the wrong host.** `GenStoryStructures` solves
   cross-chunk writes via its per-chunk `PlacePartial` pattern — which is
   what we actually want — but drags in a pile of unrelated coupling:
   - `schematicYOffsets` dict silently overwrites the schematic's own `OffsetY`
     via an obscure key format (see `WorldGenStructure.cs:84`)
   - `requireLandform` + `landformRadius` is designed for land placement,
     not coast — no clean way to express "must be open water"
   - The Surface placement branch hard-codes `Y = seaLevel + OffsetY` and
     the SurfaceRuin branch embeds at terrain — neither is what "island-style
     port at sea level with controlled depth below waterline" wants
   - `DisableSurfaceTerrainBlending` helps but doesn't clear terrain poking
     through the footprint on uneven landforms

The chunk-iterative placement *pattern* is sound — it's what lets story
structures place Lazaret and BetterRuins sunrift at 201×115×157 without
issue. We want that pattern in our own system, where landform/Y/coverage
semantics can be expressed in terms that match Seafarer's needs.

Our current `GenOceanStructures.cs` already has singleton tracking, spawn-
distance gating, and save-game persistence. Adding the chunk-iterative
placement pattern plus an ocean-coverage validator is a focused extension
of that file.

## Design

### 1. New placement mode

Extend the existing enum:

```csharp
public enum EnumOceanPlacement
{
    Underwater,
    Coastal,
    BuriedUnderwater,
    OceanSurface   // NEW
}
```

Existing modes keep their current semantics unchanged. `OceanSurface`
triggers the new reservation+per-chunk placement code path.

### 2. Reservation data type

```csharp
public class OceanStructureReservation
{
    public int OriginX;          // schematic origin world X (top-left corner after rotation)
    public int OriginY;          // schematic origin world Y (= seaLevel + def.OffsetY)
    public int OriginZ;          // schematic origin world Z
    public int VariantIndex;     // which schematic in def.Schematics was selected
    public int RotationIndex;    // 0..3 (0°, 90°, 180°, 270°)
    public int SizeX;            // cached from chosen rotation's schematic
    public int SizeY;
    public int SizeZ;
    public bool StructureRecorded;  // true once mapRegion.AddGeneratedStructure has been called
}
```

**Persistence.** Stored alongside existing `globalCounts` in savegame data:

```csharp
private Dictionary<string, OceanStructureReservation> reservations = new();
private const string ReservationsDataKey = "seafarer-ocean-reservations";
```

Loaded in `OnSaveGameLoaded`, saved in `OnGameWorldSave`, guarded by the
existing `countsLock` (same lock serves both dictionaries for simplicity —
contention is negligible).

### 3. Reservation flow

In `OnChunkColumnGen`, when iterating over `config.Structures`:

```
if def.Placement == OceanSurface:
    if reservations contains def.Code:
        -> already reserved elsewhere; SKIP to per-chunk placement step (see §4)

    # Singleton gate — existing logic, unchanged
    if def.GlobalMaxCount > 0 and globalCounts[def.Code] >= def.GlobalMaxCount:
        continue

    # Per-region maxCount gate — existing logic, unchanged
    if def.MaxCount > 0 and CountExistingStructures(mapRegion, def.Code) >= def.MaxCount:
        continue

    # Pick candidate local pos in this chunk (seeded per def.Code)
    localX, localZ = rand pick
    candidatePosX = chunkX * 32 + localX
    candidatePosZ = chunkZ * 32 + localZ

    # Spawn-distance gate — existing logic, unchanged
    if outside [MinSpawnDist, MaxSpawnDist]: continue

    # Chance gate — existing logic, unchanged
    if rand > def.Chance: continue

    # NEW: Ocean-coverage validation
    variant = cachedSchematics[def.Code][rand.Next(variants.Length)]
    rotationIdx = def.RandomRotation ? rand.Next(4) : 0
    schem = variant[rotationIdx]

    originX = candidatePosX - schem.SizeX / 2   # center-based origin (structure centered on candidate pos)
    originZ = candidatePosZ - schem.SizeZ / 2

    if !ValidateOceanCoverage(mapRegion, originX, originZ, schem.SizeX, schem.SizeZ, def):
        continue

    # Reserve
    reservations[def.Code] = new OceanStructureReservation {
        OriginX = originX,
        OriginY = sapi.World.SeaLevel + def.OffsetY,
        OriginZ = originZ,
        VariantIndex = ..., RotationIndex = rotationIdx,
        SizeX = schem.SizeX, SizeY = schem.SizeY, SizeZ = schem.SizeZ,
        StructureRecorded = false
    }
    globalCounts[def.Code] = (globalCounts.GetValueOrDefault(def.Code)) + 1
    # Will be persisted on next GameWorldSave
```

**Why center-based origin.** Spawn distance gates the candidate center
position; the schematic is placed centered on that point. Prevents a
structure whose corner is in-band from placing most of itself out-of-band.

### 4. Per-chunk placement flow

After the reservation check, every chunk that intersects a reservation's
footprint paints its slice:

```
for (code, res) in reservations:
    # Footprint cuboid in world space
    footprintMinX = res.OriginX
    footprintMaxX = res.OriginX + res.SizeX
    footprintMinZ = res.OriginZ
    footprintMaxZ = res.OriginZ + res.SizeZ

    # Current chunk's X/Z bounds
    chunkMinX = chunkX * 32
    chunkMaxX = chunkX * 32 + 32
    chunkMinZ = chunkZ * 32
    chunkMaxZ = chunkZ * 32 + 32

    if footprint does not overlap chunk: continue

    # Get the schematic for this reservation's variant+rotation
    variant = cachedSchematics[code][res.VariantIndex]
    schem = variant[res.RotationIndex]
    startPos = new BlockPos(res.OriginX, res.OriginY, res.OriginZ)

    schem.PlacePartial(
        chunks, worldgenBlockAccessor, sapi.World,
        chunkX, chunkZ,
        startPos,
        EnumReplaceMode.ReplaceAll,
        EnumStructurePlacement.Surface,   // hints to PlacePartial for surface-placement semantics
        replaceMeta: true, resolveImports: true
    )

    if !res.StructureRecorded:
        mapRegion.AddGeneratedStructure(new GeneratedStructure {
            Code = code,
            Group = "ocean",
            Location = new Cuboidi(res.OriginX, res.OriginY, res.OriginZ,
                                   res.OriginX + res.SizeX - 1,
                                   res.OriginY + res.SizeY - 1,
                                   res.OriginZ + res.SizeZ - 1),
            SuppressTreesAndShrubs = def.SuppressTrees,
            SuppressRivulets = true
        })
        res.StructureRecorded = true
        # Will be persisted on next GameWorldSave
```

`PlacePartial` internally clips schematic writes to the current chunk
(`BlockSchematicPartial.cs:76`), so only the slice of the schematic that
falls in this chunk is written. Subsequent chunk-gens paint their slices.

The `StructureRecorded` flag prevents duplicate `AddGeneratedStructure`
calls across the many chunks that will intersect the footprint — the
generated-structure record represents the *whole* structure, not a slice.

### 5. Ocean-coverage validator

```csharp
private bool ValidateOceanCoverage(IMapRegion mapRegion, int originX, int originZ, int sizeX, int sizeZ, OceanStructureDef def)
{
    // 9 sample points: center, 4 corners, 4 edge midpoints
    int cx = originX + sizeX / 2;
    int cz = originZ + sizeZ / 2;
    int[][] samples = new[] {
        new[]{ cx, cz },                               // center
        new[]{ originX, originZ },                     // corners
        new[]{ originX + sizeX, originZ },
        new[]{ originX, originZ + sizeZ },
        new[]{ originX + sizeX, originZ + sizeZ },
        new[]{ cx, originZ },                          // edge midpoints
        new[]{ cx, originZ + sizeZ },
        new[]{ originX, cz },
        new[]{ originX + sizeX, cz }
    };

    int oceanSamples = 0;
    bool centerOcean = false;
    bool cornersOcean = true;

    for (int i = 0; i < samples.Length; i++)
    {
        float oceanicity = GetOceanicity(mapRegion, samples[i][0], samples[i][1]);
        bool isOcean = oceanicity > 0;
        if (isOcean) oceanSamples++;

        if (i == 0) centerOcean = isOcean;
        if (i >= 1 && i <= 4 && !isOcean) cornersOcean = false;
    }

    // Moderate rule: center + corners must be ocean; >= 7 of 9 total
    if (!centerOcean || !cornersOcean) return false;
    if (oceanSamples < 7) return false;

    // Center water depth must satisfy MinWaterDepth/MaxWaterDepth
    int seaLevel = sapi.World.SeaLevel;
    var mapChunk = sapi.WorldManager.GetMapChunk(cx / 32, cz / 32);
    if (mapChunk == null) return true;  // neighbor chunk not yet available; allow (coarse filter via OceanMap already passed)

    int terrainHeight = mapChunk.WorldGenTerrainHeightMap[(cz % 32) * 32 + (cx % 32)];
    int waterDepth = seaLevel - terrainHeight;
    if (def.MinWaterDepth > 0 && waterDepth < def.MinWaterDepth) return false;
    if (def.MaxWaterDepth > 0 && waterDepth > def.MaxWaterDepth) return false;

    return true;
}
```

`GetOceanicity` already exists in `GenOceanStructures.cs` — it queries
`IMapRegion.OceanMap` with bilinear interpolation at a world position.
This coarse-grained ocean data is available for any map region, not just
currently-generating chunks.

**Cross-region samples.** If a sample point falls outside `mapRegion`
(structure footprint near a region boundary), we currently have no
built-in multi-region sample path. First-pass acceptance: if all samples
that ARE in the current region pass, accept. If we see regressions
(structure reserving at region edges then failing to find neighbor-region
data), add cross-region sampling.

### 6. Y computation

Inside the reservation flow:

```csharp
OriginY = sapi.World.SeaLevel + def.OffsetY
```

That's it. No schematicYOffsets dict, no base-game clobber. Tortuga sets
`"offsetY": -27` in its `oceanstructures.json` entry and that value is
read directly.

### 7. Existing placement modes unchanged

`Underwater`, `Coastal`, `BuriedUnderwater` retain their current
single-chunk-placement semantics. They've been working fine for the
wreck schematics (which fit in one chunk column). Only the new
`OceanSurface` branch gets the reservation+per-chunk pattern.

### 8. Tortuga re-integration

**Remove from `storystructures.json`:**

Delete the `tortuga` structure entry (keep `potatoking` — story structures
are the right fit for it; it's small and land-based).

Also remove the `"story/seafarer:tortuga": -27` entry from `schematicYOffsets`.

**Add to `oceanstructures.json`:**

```json5
{
  "code": "tortuga",
  "schematics": ["costal/tortuga"],
  "placement": "oceansurface",
  "chance": 1.0,
  "minWaterDepth": 5,
  "maxWaterDepth": 40,
  "offsetY": -27,
  "globalMaxCount": 1,
  "minSpawnDist": 500,
  "maxSpawnDist": 3000,
  "suppressTrees": true,
  "randomRotation": true
}
```

`chance: 1.0` means every chunk in the spawn band is considered for
reservation. Validation does the real filtering — most candidates will
fail the coverage check; the first that passes wins. Singleton gate
ensures only one ever reserves.

**Switch `map-tortuga.json` back to `ItemOceanLocatorMap`:**

```json5
{
  "code": "map-tortuga",
  "class": "ItemOceanLocatorMap",
  ...
  "attributes": {
    ...
    "searchRange": 10000,
    "locatorPropsbyType": {
      "*": {
        "schematiccode": "tortuga",
        "waypointtext": "location-tortuga",
        "waypointicon": "x",
        "waypointcolor": [0.95, 0.75, 0.2, 1],
        "randomX": 15,
        "randomZ": 15
      }
    }
  },
  "shape": { "base": "game:item/clutter/fishing/bottlemessage" },
  ...
}
```

`ItemOceanLocatorMap` resolves locations via
`ModSystemStructureLocator.FindFreshStructureLocation`, which reads
`mapRegion.GeneratedStructures` — we populate those records via our
`AddGeneratedStructure` call in the per-chunk flow. `searchRange: 10000`
ensures the bottle finds Tortuga from anywhere in the spawn-dist band.

## Scope excluded (YAGNI)

- **No changes to `Underwater`/`Coastal`/`BuriedUnderwater` placements** —
  wreck schematics work fine as they are
- **No Harmony patches** — pure mod-space code
- **No cross-region ocean sampling** — first-pass uses single-region only;
  add if edge-cases appear in testing
- **No landform filtering** — `OceanMap` sampling is sufficient
- **No changes to Potato King** — stays in story structures
- **No retry-until-success loop inside one chunk-gen event** — if the
  current chunk's candidate fails, we don't try more candidates in the
  same event. The next chunk in the band gets its own try. Eventual
  coverage guaranteed by the 1.0 chance + large band

## Known tensions / verification

**Chunk-gen ordering.** The first chunk in the spawn band to pass
validation wins the reservation. If chunks generate in spiral order
around spawn, Tortuga tends to land at the closest valid coastal spot.
That's a feature, not a bug — matches "near spawn" intent.

**Partial placement visibility.** A player exploring into Tortuga's
footprint sees slices appear as neighbor chunks generate. Structure
appears complete once all overlapping chunks have generated. Same
behavior base-game Lazaret/village exhibit — not a new UX concern.

**Singleton + rollback.** If a player's world corrupts or they restore
a backup, `globalCounts` and `reservations` persist with savegame.
Restoring pre-reservation state allows Tortuga to re-roll. Acceptable.

**Ocean-map sampling accuracy.** `OceanMap` is coarse (region-level
grid). A 178-block footprint may span a few grid cells. `GetOceanicity`
bilerps between cells, so sub-cell fidelity is reasonable for an
"is this ocean" binary check.

## Files changed / created

| File | Change |
|---|---|
| `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs` | Modify — add `OceanSurface` enum value, reservation dict + save-data plumbing, `ValidateOceanCoverage`, reservation flow branch, per-chunk placement flow |
| `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json` | Modify — add `tortuga` entry with `placement: "oceansurface"` |
| `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json` | Modify — remove `tortuga` structure entry, remove its `schematicYOffsets` line |
| `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json` | Modify — switch class back to `ItemOceanLocatorMap`, restore `searchRange: 10000` + `locatorPropsbyType` structure |

## Testing plan

1. **Build**: `dotnet build Seafarer/Seafarer/Seafarer.csproj` — succeeds.
2. **Asset validator**: `python3 validate-assets.py` — no new errors.
3. **Fresh world**: create new world. Server log on player join should
   show Tortuga reservation being made (add a `Mod.Logger.Notification`
   when the reservation succeeds). Record the reported origin coords.
4. **Teleport and inspect**: `/tp <origin x> 130 <origin z>`. Walk the
   entire 178×171 footprint. Verify no gaps in blocks — cross-chunk
   placement fully paints the schematic. Absence of "Tried to set block
   outside generating chunks" warnings confirms the fix.
5. **Map activation**: give yourself `map-tortuga` in creative. Right-
   click. Expected: waypoint added (structure's `GeneratedStructure`
   record is discoverable via `FindFreshStructureLocation` at
   `searchRange: 10000`).
6. **Save/load**: quit cleanly, reload. Verify reservation persists
   and any unplaced chunks paint their slices when generated.
7. **Regression — wreck placement**: verify `wreck-crimsonrose` and
   `wreck-one` still place via `Underwater` mode (unchanged code path).
8. **Regression — Potato King**: verify Potato King still places
   correctly via story structures (unchanged).
9. **Regression — fishing/panning drops**: `map-tortuga` still drops
   from saltwater/reef fish and sand panning.
