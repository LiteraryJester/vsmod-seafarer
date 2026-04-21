# Tortuga Port Hub — World-Singleton Worldgen Design

**Date:** 2026-04-19
**Status:** Approved

## Summary

Add the Tortuga port hub — a large (201×56×196) coastal landmark schematic — to
Seafarer's world generation as a **true world-singleton** landmark, spawning in
shallow ocean water 500–3000 blocks from world spawn. A future map item (out of
scope for this spec) will guide players to it.

The implementation extends the existing `GenOceanStructures` system with three
new per-structure fields: `GlobalMaxCount`, `MinSpawnDist`, `MaxSpawnDist`, plus
persistent savegame tracking of globally-placed counts.

## Motivation

Seafarer's ocean structure system currently only supports per-region
`MaxCount`. Large worlds would contain many Tortugas, which is wrong for a
landmark port hub. We want exactly one per world, positioned so that players
can reach it via early/mid-game seafaring expeditions.

Base-game story structures (`storystructures.json`) already solve one-per-world
tracking, but their placement modes (`surface`, `underground`, `SurfaceRuin`)
and `requireLandform` constraints don't understand ocean / beach / water depth.
Tortuga is fundamentally a coastal structure, so we extend the ocean structure
system rather than patch the story structure system.

## Design

### Config entry (`assets/seafarer/worldgen/oceanstructures.json`)

Add a new structure entry:

```json5
{
  code: "tortuga",
  schematics: ["costal/tortuga"],
  placement: "underwater",
  chance: 0.05,
  minWaterDepth: 3,
  maxWaterDepth: 15,
  offsetY: 0,
  globalMaxCount: 1,
  minSpawnDist: 500,
  maxSpawnDist: 3000,
  suppressTrees: true,
  randomRotation: true
}
```

**Placement rationale:**

- `placement: "underwater"` — Tortuga's lower structure (sand, sea floor, seagrass,
  seaweed) lives in water; the port superstructure rises above. Sitting it in
  shallow ocean achieves that silhouette.
- `minWaterDepth: 3 / maxWaterDepth: 15` — ensures the placement point is real
  ocean (not shoreline) yet shallow enough that the 56-block-tall schematic
  emerges clearly above the waterline.
- `chance: 0.05` — combined with the singleton flag, this spreads placement
  across the valid band rather than clustering at the nearest eligible chunk.
  A future map item guides players to the actual location regardless of exact
  coordinates.
- `minSpawnDist: 500, maxSpawnDist: 3000` — radial distance from world spawn.
  Close enough for mid-game expeditions, far enough to feel like a real journey.

### Code changes (`WorldGen/GenOceanStructures.cs`)

#### Extend `OceanStructureDef`

```csharp
public int GlobalMaxCount = 0;  // 0 = unlimited (per-world)
public int MinSpawnDist = 0;    // radial blocks from world spawn
public int MaxSpawnDist = 0;    // 0 = unlimited
```

#### Add to `GenOceanStructures`

```csharp
private readonly Dictionary<string, int> globalCounts = new();
private readonly object countsLock = new();
private const string CountsDataKey = "seafarer-ocean-structure-counts";
```

#### Lifecycle hooks (registered in `StartServerSide`)

- `sapi.Event.SaveGameLoaded` → deserialize `globalCounts` from savegame data
  (key: `seafarer-ocean-structure-counts`). On a fresh world, initialize empty.
- `sapi.Event.GameWorldSave` → serialize `globalCounts` back to savegame data.

Serialization uses `SerializerUtil.Serialize` / `Deserialize` with
`Dictionary<string, int>`.

#### Placement gates (`OnChunkColumnGen`)

Add two new gates to the existing placement flow. Order matters — cheap checks
first for early-out:

1. **Chance roll** (unchanged) — `rand.InitPositionSeed(...)`, compare to `Chance`.
2. **Global singleton gate (NEW)** — under `countsLock`, skip if
   `def.GlobalMaxCount > 0 && globalCounts[def.Code] >= def.GlobalMaxCount`.
3. **Per-region `MaxCount` gate** (unchanged).
4. **Pick local position** → compute world `posX / posZ`.
5. **Spawn-distance gate (NEW)** — compute radial distance from
   `sapi.World.DefaultSpawnPosition`. Skip if outside
   `[MinSpawnDist, MaxSpawnDist]`. Uses Euclidean distance, not the
   axis-separated band base-game story structures use.
6. **Oceanicity / beach / water-depth validation** (unchanged).
7. **Place schematic** (unchanged).
8. **Increment counter (NEW)** — under `countsLock`, `globalCounts[def.Code]++`.
9. **Record generated structure on map region** (unchanged).

#### Admin command behavior

The existing `/ocean place <code>` command does **not** increment
`globalCounts`. Creative / testing placements don't consume the singleton
budget. This means an admin can `/ocean place tortuga` multiple times for
testing without preventing natural world-gen placement from happening later.

### Thread safety

`ChunkColumnGeneration` callbacks run on a worker thread. All reads and writes
to `globalCounts` happen under `countsLock`. Save/load happens on the main
server thread, also under the lock.

### Save / load persistence

- New worlds: `globalCounts` starts empty; first natural placement increments
  to 1 and persists on next save.
- Existing worlds with the mod: no count data exists yet → treated as 0, so
  Tortuga can still spawn on first chunk-gen.
- World reload after placement: count loads as 1, singleton gate blocks further
  placement.
- World backup restore to pre-Tortuga state: count resets to 0; Tortuga can
  regenerate. This matches player intent when restoring a backup.

## Scope — excluded (YAGNI)

- No `requireLandform` — water depth / oceanicity already filters terrain
- No `dependsOnStructure` linkage — Tortuga is independent of base-game lore
- No build protection, claim regions, or ownership — can be added later
- No custom rock/block remapping — Tortuga uses fixed block palette
- No multi-sample footprint validation (see Known Limitations)
- No map item — separate feature

## Known limitations

**Single-point placement validation.** The existing placement code checks
oceanicity and water depth at one sample point per chunk, not across the
201×196 footprint. On jagged coastlines, one end of Tortuga could land in
deep water while the other sits on shore. The `minWaterDepth: 3` /
`maxWaterDepth: 15` band biases toward gentle shelves, making this rare, but
not impossible.

If testing shows frequent bad placements, a follow-up change can add
multi-sample footprint validation (e.g., sample the 4 corners and midpoints,
require all to pass). Out of scope for this spec.

## Files changed

| File | Change |
|---|---|
| `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json` | Add `tortuga` structure entry |
| `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs` | Extend `OceanStructureDef`; add singleton + distance gates; add save/load hooks |

## Testing plan

1. **Build**: `dotnet build Seafarer/Seafarer.csproj` succeeds.
2. **Fresh world gen**: Create a new world; `/ocean list` shows `tortuga`.
   Explore 500–3000 blocks of coastline; verify exactly one Tortuga appears.
3. **Save/load persistence**: After confirming placement, quit and reload;
   `/ocean list` still reports the structure; no second Tortuga spawns in
   newly-generated chunks.
4. **Admin command**: `/ocean place tortuga` places at current position
   without changing the natural-gen singleton status.
5. **Distance constraint**: Use `/worldconfig setworldspawn` or inspect world
   spawn; verify placement falls in the 500–3000 band.
6. **Asset validation**: `python3 vs_validators/validate-assets.py` reports
   no new errors.
