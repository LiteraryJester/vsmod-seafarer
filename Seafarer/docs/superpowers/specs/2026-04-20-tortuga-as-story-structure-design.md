# Tortuga as Story Structure — Design

**Date:** 2026-04-20
**Status:** Approved

## Summary

Move Tortuga (201×56×196) out of Seafarer's custom `GenOceanStructures`
system and into the base-game `GenStoryStructures` system, alongside the
Potato King's House. This unblocks placement of the full schematic — the
ocean-placement path (via `ChunkColumnGeneration` + `worldgenBlockAccessor`)
cannot write across chunk boundaries, which was silently truncating
Tortuga's footprint and producing "Tried to set block outside generating
chunks" warnings with gaps in the placed structure.

The switch also simplifies the item wiring: `map-tortuga` moves from our
mod-specific `ItemOceanLocatorMap` class to base-game `ItemLocatorMap`,
which reads directly from the story-structure dictionary.

## Motivation

Runtime logs during in-game testing:

```
Tried to set block outside generating chunks! Set RuntimeEnv.DebugOutOfRangeBlockAccess to debug.
```

This is VS telling us that block-set calls outside the currently-
generating chunks are being dropped. Tortuga's 201×196 footprint is
~6 chunks wide, so any placement overlaps 5–6 chunks that haven't
started generating yet when `ChunkColumnGeneration` fires for the
origin chunk.

`GenStoryStructures` avoids this by using `api.Event.WorldgenHook`
(rather than `ChunkColumnGeneration`). The hook runs once per
structure, after the game has pre-loaded every chunk in the target
region. BetterRuins uses this system for structures up to
157×115×201 (brsunrift) — including the `ExcludeSchematicSizeProtect`
flag to bypass base-game's default size-protection cap.

## Design

### 1. Remove Tortuga from `oceanstructures.json`

Delete the entire `tortuga` entry (currently the third structure in
the file). The remaining two entries — the Crimson Rose and "wreck-one"
wrecks — keep the ocean system for what it's good at: small
submerged/coastal structures that fit inside a single chunk column.

The `OceanStructureDef` fields `GlobalMaxCount`, `MinSpawnDist`,
`MaxSpawnDist` and the associated code in `GenOceanStructures.cs`
are kept in place — they still work, just have no current users.
No reason to rip out working code.

### 2. Add Tortuga to `storystructures.json`

Add a new structure entry alongside the existing `potatoking` one:

```json5
{
  "code": "tortuga",
  "group": "storystructure",
  "name": "Tortuga",
  "schematics": ["costal/tortuga"],
  "placement": "surface",
  "UseWorldgenHeight": true,
  "ExcludeSchematicSizeProtect": true,
  "dependsOnStructure": "spawn",
  "minSpawnDistX": -3000,
  "maxSpawnDistX":  3000,
  "minSpawnDistZ": -3000,
  "maxSpawnDistZ":  3000,
  "requireLandform": "flat",
  "landformRadius": 250,
  "generateGrass": true,
  "skipGenerationCategories": {
    "structures": 300,
    "trees": 200,
    "shrubs": 150,
    "hotsprings": 250,
    "patches": 100,
    "pond": 200,
    "rivulets": 100
  }
}
```

**Placement mode.** `"surface"` + `"UseWorldgenHeight": true` sits the
schematic on the current terrain height (not sea level, not embedded).
Because the Tortuga schematic includes its own sand/seafloor base, this
produces the intended island appearance: the structure looks like an
island that grows out of the terrain.

`"SurfaceRuin"` was considered but rejected — it embeds the structure
so its top is at terrain level, burying the lower half. Wrong for a
building with its own foundation.

**`ExcludeSchematicSizeProtect: true`.** BetterRuins' brsunrift
(157×115×201) uses this flag; it tells `GenStoryStructures` to bypass
its size-protection cap (which prevents casually huge placements from
being attempted). Without it, Tortuga's size may trigger rejection.

**Landform.** `"flat"` (not `"veryflat"`) — a 250-block-radius flat
patch is hard to find in `"veryflat"` even on generous terrain seeds.
`"flat"` matches typical coastal plain geometry.

**Spawn distance.** `±3000 X/Z` band overlaps the prior 500–3000 radial
band for continuity. Story structures use axis-aligned bands, so the
radial band doesn't translate exactly — this box is slightly more
permissive at the corners, slightly more restrictive near axes. OK.

**`skipGenerationCategories`.** Wide buffers around the placed
structure — prevents base-game trees, shrubs, ponds, and rivulets from
generating inside Tortuga's footprint or immediately adjacent to it.
BetterRuins uses similar values for brcrossroads and brsunrift.

### 3. Keep the schematic at `costal/tortuga`

Don't rename/move the 18 MB schematic file to a `story/` directory.
Story structures accept any schematic path — the `"story/"` prefix in
base-game config is convention, not a requirement.

### 4. Switch `map-tortuga` to `ItemLocatorMap`

`map-tortuga.json` currently uses `ItemOceanLocatorMap`. Since the
structure no longer registers via our ocean system, change the class
and restructure the attributes to match base-game expectations.

**Before:**
```json5
{
  "code": "map-tortuga",
  "class": "ItemOceanLocatorMap",
  "attributes": {
    ...
    "searchRange": 10000,
    "locatorPropsbyType": {
      "*": {
        "schematiccode": "tortuga",
        ...
      }
    }
  },
  ...
}
```

**After:**
```json5
{
  "code": "map-tortuga",
  "class": "ItemLocatorMap",
  "attributes": {
    ...
    "locatorProps": {
      "schematiccode": "tortuga",
      "waypointtext": "location-tortuga",
      "waypointicon": "x",
      "waypointcolor": [0.95, 0.75, 0.2, 1],
      "randomX": 15,
      "randomZ": 15
    }
  },
  ...
}
```

- `ItemOceanLocatorMap` → `ItemLocatorMap` (base game)
- `locatorPropsbyType` (wildcard) → `locatorProps` (base class uses
  singular)
- Drop `searchRange` — base `ItemLocatorMap` doesn't read it; its
  `OnHeldInteractStart` uses `Structures.Get(code)`, which is a direct
  dictionary lookup keyed on `structure.code` — no proximity limit
- `schematiccode: "tortuga"` stays unchanged (matches our
  `structure.code`)

**Why this works with story structures.** `ItemLocatorMap.OnHeldInteractStart`
at line 93–97 of `ItemLocatorMap.cs`:

```csharp
var val = storyStructures.Structures.Get(props.SchematicCode);
if (val != null)
{
    pos = val.CenterPos.ToVec3d().Add(0.5, 0.5, 0.5);
}
```

This is the player-held-use path. `storyStructures.Structures` is a
`Dictionary<string, StoryStructureLocation>` keyed on `structure.code`
from `storystructures.json`. Once Tortuga is registered as a story
structure, the map activates from anywhere in the world (no
search-range requirement).

## Scope excluded (YAGNI)

- **No changes to `ItemOceanLocatorMap`** — `map-crimsonrose` still
  uses it
- **No changes to fishing/panning drop patches** — `map-tortuga` still
  drops from saltwater/reef fish and sand panning
- **No removal of `GlobalMaxCount`, `MinSpawnDist`, `MaxSpawnDist`**
  from `OceanStructureDef` — working code, no current users
- **No rename of the schematic file** — 18 MB file stays at
  `costal/tortuga.json`
- **No lang changes** — `item-map-tortuga` and `location-tortuga`
  already exist in `en.json`

## Known tensions / verification

**Landform availability.** `"flat"` with `landformRadius: 250` — if
the spawn-distance band falls on terrain where no 250-block "flat"
patch exists, `GenStoryStructures` logs `FailedToGenerateLocation`
and the structure never spawns. Base-game surfaces this as an
admin warning on player login. Fall back: widen to `landformRadius: 400`
or relax to the superset landform if one exists.

**Story placement takes over singleton tracking.** Our
`GenOceanStructures.globalCounts` savegame data still exists but
won't track Tortuga anymore. If you reload an existing world that
had Tortuga placed via the old ocean system, the old instance stays
in the world (block data is persisted in chunks). The story system
treats the world as "no Tortuga placed yet" and places a second
one. Use `/wgen story rmsc tortuga` to reset state, or test in a
fresh world. This matches the behavior we hit during Potato King
iteration.

**`FindFreshStructureLocation` path.** `ItemLocatorMap` also calls
this for trader-sold maps, and its matcher splits on `/` and checks
`parts[1]`. With stored code `"tortuga:costal/tortuga"`, split →
`["tortuga:costal", "tortuga"]`, `parts[1] == "tortuga"` matches
our `schematiccode: "tortuga"`. Trader flow works too, for future use.

## Files changed

| File | Change |
|---|---|
| `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json` | Remove `tortuga` entry |
| `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json` | Add `tortuga` entry |
| `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json` | Change class, restructure attributes |

## Testing plan

1. **Build**: `dotnet build Seafarer/Seafarer/Seafarer.csproj` — succeeds.
2. **Asset validator**: `python3 validate-assets.py` — no new errors.
3. **Fresh world**: create new world; on player join, server log should
   show no `FailedToGenerateLocation` for `tortuga`. If present, widen
   `landformRadius` and retry.
4. **Teleport**: `/tpstoryloc tortuga` teleports to the placed
   structure. Verify the structure is fully present — walk the entire
   perimeter (~200 blocks each side) and look for truncation or gaps.
   Absence of "Tried to set block outside generating chunks" warnings
   in the log confirms the cross-chunk-write issue is resolved.
5. **Map activation from spawn**: obtain `map-tortuga` via creative or
   the fishing/panning drops. Right-click the map while standing at
   spawn. Expected: "Approximate location of Tortuga added to your
   world map" — no search-range failure (the base-class path uses
   direct dictionary lookup).
6. **Regression — fishing/panning drops**: spawn saltwater fish and
   pan sand; confirm `map-tortuga` still drops at the configured rates.
7. **Regression — crimson rose map**: verify `map-crimsonrose` still
   works (uses `ItemOceanLocatorMap`, unchanged path).
