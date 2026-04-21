# Ocean Story Structures — Design

**Date:** 2026-04-20
**Status:** Approved

## Summary

Add a `storyStructure: true` flag to `OceanStructureDef`. Structures with
this flag reserve their location at world init (via `SaveGameLoaded`),
guaranteeing every future chunk that touches the footprint will paint its
slice. Structures without the flag (default) keep the current per-chunk
lazy reservation behavior.

Apply the flag to `wreck-crimsonrose` and `tortuga` so both place reliably
from the start. Leave `wreck-one` on the per-chunk path — it's the reference
case for future non-story ocean structures.

## Motivation

The current OceanSurface implementation reserves lazily: the first chunk in
the spawn band whose random roll passes creates the reservation and saves it.
But chunks generated BEFORE that winning roll have already run — they never
consult the reservation, so any portion of Tortuga's footprint that
overlapped an already-generated chunk is permanently empty. The user reported
"3/4 of Tortuga generates" for exactly this reason: spawn chunks generated
without seeing a reservation, a later chunk finally made one, and the missing
3/4 is the already-generated region.

Story structures solve this in base-game by running `DetermineStoryStructures`
at `InitWorldGen` time — BEFORE any chunk generates. Every subsequent chunk
sees the reservation and paints its slice.

We need the same pattern for our critical ocean structures. The wreck of
the Crimson Rose is also worth making "story" — it's required for the
crimson-rose map and related quest content, so reliable placement matters
more than placement variety.

## Design

### 1. New `OceanStructureDef` field

Add a single boolean:

```csharp
// When true, reservation happens at world init (SaveGameLoaded) rather
// than lazily per-chunk. Placement is always chunk-iterative via
// BlockSchematicPartial.PlacePartial.
public bool StoryStructure = false;
```

JSON field: `"storyStructure": true` (Newtonsoft default PascalCase → camelCase).

### 2. Deferred-Y field on `OceanStructureReservation`

`OceanSurface` placement resolves Y at reservation time (`seaLevel + offsetY`).
Other placement modes (`Underwater`, `Coastal`, `BuriedUnderwater`) depend on
per-chunk `WorldGenTerrainHeightMap` data that isn't available until the
chunk containing the origin first generates. So the reservation stores an
"unresolved" sentinel and we fix it up lazily:

```csharp
[ProtoContract]
public class OceanStructureReservation
{
    // existing fields …
    [ProtoMember(10)] public bool OriginYResolved;  // NEW
}
```

`OriginYResolved` defaults to `false`. For OceanSurface reservations it
is set to `true` immediately at reservation time. For Underwater/Coastal/
BuriedUnderwater, it is set to `true` the first time the chunk containing
the reservation's XZ origin generates and we resolve Y from terrain.

### 3. Init-time reservation flow

New method `DetermineOceanStoryStructures()` called from `OnSaveGameLoaded`
AFTER both dicts are deserialized:

```
for each structure def with StoryStructure == true:
    if reservations[def.Code] already exists:
        continue            # persisted from previous save

    if GlobalMaxCount > 0 and globalCounts >= GlobalMaxCount:
        continue            # already placed in a different session (defensive)

    spawnPos = GetSpawnPosSafe()
    if spawnPos is null:
        log warning "spawn not yet determined; story reservation skipped"
        continue            # will retry next load

    for attempt in 0..MaxReservationAttempts (30):
        pick random position in [MinSpawnDist .. MaxSpawnDist] band around spawnPos
        force-load map region via sapi.WorldManager.BlockingTestMapRegionExists(rx, rz)
        get mapRegion
        pick variant + rotation (seeded by (def.Code, attempt))
        compute center-based origin (originX, originZ) for that footprint
        if !ValidateOceanCoverage(mapRegion, originX, originZ, sizeX, sizeZ, def):
            continue

        # success - build reservation
        reservation = new OceanStructureReservation {
            OriginX = originX,
            OriginZ = originZ,
            OriginY = (def.Placement == OceanSurface) ? (seaLevel + def.OffsetY) : 0,
            OriginYResolved = (def.Placement == OceanSurface),
            VariantIndex, RotationIndex, SizeX/Y/Z,
            StructureRecorded = false
        }
        reservations[def.Code] = reservation
        globalCounts[def.Code]++
        log notification
        break    # next def

    if no attempt succeeded:
        log warning "Ocean story structure '{code}' failed to find a valid spot after {attempts} attempts; will not generate this world."
```

`MaxReservationAttempts = 30` — tunable constant; 30 is enough to find an
open-water spot in the band without force-loading excessive regions.

### 4. Per-chunk placement flow (updated)

`HandleOceanSurface` currently does Phase A (reserve if none) + Phase B
(place slice). For story structures the reservation already exists at this
point, so Phase A becomes a no-op. To keep the code focused, we'll also
introduce a tiny **Y-resolution step** before placement for reservations
whose Y is not yet resolved:

```
for each reservation whose footprint intersects current chunk:
    if !res.OriginYResolved:
        if current chunk does NOT contain (res.OriginX, res.OriginZ) origin corner:
            continue    # defer to the chunk that contains origin
        # resolve Y from this chunk's terrain, based on def.Placement
        int terrainHeight = mapChunk.WorldGenTerrainHeightMap[(res.OriginZ % 32) * 32 + (res.OriginX % 32)]
        int waterDepth = seaLevel - terrainHeight
        if !IsValidPlacement(def, oceanicity, beachStrength, waterDepth):
            # terrain here doesn't actually satisfy the def constraints
            # drop this reservation (effectively cancel). log warning.
            reservations.Remove(def.Code); globalCounts[def.Code]--; save; continue
        res.OriginY = CalculatePlacementY(def, terrainHeight, schematic)
        res.OriginYResolved = true
        # mark dirty for save

    # Y is resolved; do PlacePartial normally
    PlacePartial(...)
    if !res.StructureRecorded: record and flip flag atomically
```

### 5. Non-story ocean structures — unchanged

Structures with `storyStructure: false` (default) keep the current
per-chunk behavior. Any future small ocean structure that works fine with
"lazy" reservation still uses `HandleLegacyPlacement` (for underwater/
coastal/buried-underwater) or the current lazy `OceanSurface` code path.

Wait — currently, non-story `OceanSurface` structures go through
`HandleOceanSurface` which does Phase A (lazy reserve) + Phase B (place
slice). We keep that path. The only difference for `StoryStructure: true`
is that `DetermineOceanStoryStructures` pre-creates the reservation at
init, so Phase A is a no-op.

For clarity: the ONLY code change in `HandleOceanSurface`'s reservation
phase is an additional "skip if story" check to prevent non-story-path
reservation attempts from racing against a story reservation that might
be mid-creation. Since story reservation runs in `OnSaveGameLoaded`
before chunk gen starts, there's no real race — but the check makes the
intent explicit.

### 6. Config changes

`assets/seafarer/worldgen/oceanstructures.json`:

```json5
{
  "structures": [
    {
      "code": "wreck-crimsonrose",
      ...
      "storyStructure": true   // NEW
    },
    {
      "code": "wreck-one",
      ...
      // storyStructure absent (defaults to false)
    },
    {
      "code": "tortuga",
      ...
      "storyStructure": true   // NEW
    }
  ]
}
```

## Files changed

| File | Change |
|---|---|
| `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs` | Add `StoryStructure` field; add `OriginYResolved` field (with `[ProtoMember(10)]`); add `DetermineOceanStoryStructures` method + hook into `OnSaveGameLoaded`; add Y-resolution step in per-chunk placement path; skip lazy-reservation for story defs |
| `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json` | Add `"storyStructure": true` to `wreck-crimsonrose` and `tortuga` |

## Scope excluded (YAGNI)

- **No changes to `IsValidPlacement` / `ValidateOceanCoverage`** — both work
  as-is
- **No changes to other structure definitions** — `wreck-one` stays lazy;
  future non-story ocean structures use the existing code path
- **No worker-thread reservation** — `DetermineOceanStoryStructures` runs on
  the save-game-loaded thread (main), same as base-game
  `DetermineStoryStructures`
- **No cross-structure dependency** (`dependsOnStructure`) — out of scope;
  add if we need chained positioning in the future
- **No custom reservation placement** — reservation uses the normal
  spawn-distance band + ocean-coverage validator; no special story-spot logic

## Known tensions / verification

**Y resolution failure mode.** If `DetermineOceanStoryStructures` picks an
XZ that passes `ValidateOceanCoverage` (OceanMap coarse check) but the
actual per-chunk terrain doesn't satisfy the placement type's `IsValidPlacement`
constraint (e.g., water depth out of range for Underwater), the reservation
is dropped at first-chunk-gen time. The structure silently fails to generate
this world. A log warning makes this visible. We accept this edge case —
it should be rare given the coarse OceanMap check is already strict enough
for most cases.

**Reservation scan timing.** `OnSaveGameLoaded` fires after savegame data is
loaded. `sapi.World.DefaultSpawnPosition` should be set by this point for
existing worlds (loaded from save). For brand-new worlds, the spawn may not
yet be determined — the scan retries on subsequent load if `GetSpawnPosSafe`
returns null. A fresh world's first load + immediate quit would leave no
reservations, but subsequent loads retry. (Note: base-game story structures
also handle this via `data.Contains(code)` flag; we use reservation dict
presence as the equivalent "already attempted" marker.)

**Force-loading map regions.** `sapi.WorldManager.BlockingTestMapRegionExists`
triggers map-region generation if not present. This is the same technique
base-game story structures use. Map regions are cheap (region-level data
only; no chunk blocks are generated). Force-loading up to 30 candidate
regions per structure × 2 structures = 60 region loads max, but typically
<10 before a valid one is found.

## Testing plan

1. Build: `dotnet build Seafarer/Seafarer/Seafarer.csproj` — 0 errors.
2. Asset validator: baseline (1 pre-existing unrelated error).
3. **Fresh world (primary)**: Start new world. Server log should show:
   - `Ocean story structure 'wreck-crimsonrose' reserved at (X, Y, Z)`
   - `Ocean story structure 'tortuga' reserved at (X, Y, Z)` (Y may be 0 for
     wreck-crimsonrose at first — resolved lazily)
   - NO `Tried to set block outside generating chunks` warnings
4. `/tp <tortuga X> 130 <tortuga Z>` → walk full footprint; no gaps.
5. `/tp <wreck-crimsonrose X> <Y> <Z>` → wreck fully present on seafloor.
6. Save/reload: both reservations persist; no duplicate placement on reload.
7. Regression — `wreck-one` still spawns lazily per-chunk at 1.5% chance.
8. Regression — `map-tortuga` and `map-crimsonrose` both activate to
   correct waypoints.
