# Potato King's House — Worldgen Design

**Date:** 2026-04-20
**Status:** Approved

## Summary

Add the Potato King's House (27×23×29 surface schematic) to world generation
as a singleton landmark within ±2500 blocks of world spawn, and wire the
existing `map-potato` item (already given by Celeste's quest dialogue) to
point at it.

Implementation piggybacks on base-game `GenStoryStructures` via a new
`seafarer:worldgen/storystructures.json` — the game's asset loader merges
all mod storystructure configs at init.

## Motivation

The Potato King's House schematic has been authored but is not yet placed in
the world, and `map-potato` currently references a non-existent structure
code (`"buriedtreasurechest"`), so the Celeste-delivered map never activates
for players. Fixing both loose ends in one change gets the Potato King quest
path working end-to-end.

Using story structures (rather than extending `GenOceanStructures`) trades
worldgen code duplication for a small dependency on a base-game system that
already handles singletons, landform filtering, and spawn-distance bands.

## Design

### 1. New worldgen config (`assets/seafarer/worldgen/storystructures.json`)

```json5
{
  "schematicYOffsets": {
    "surface/potato-king-house": -1
  },
  "structures": [
    {
      "code": "potatoking",
      "group": "storystructure",
      "name": "Potato King's House",
      "schematics": ["surface/potato-king-house"],
      "placement": "surface",
      "dependsOnStructure": "spawn",
      "minSpawnDistX": -2500,
      "maxSpawnDistX":  2500,
      "minSpawnDistZ": -2500,
      "maxSpawnDistZ":  2500,
      "requireLandform": "veryflat",
      "landformRadius": 80,
      "generateGrass": true,
      "skipGenerationCategories": {
        "structures": 80,
        "trees": 50,
        "shrubs": 50,
        "hotsprings": 100,
        "patches": 30
      }
    }
  ]
}
```

**How the loader picks this up.** `GenStoryStructures.Init` calls
`api.Assets.GetMany<WorldGenStoryStructuresConfig>(api.Logger, "worldgen/storystructures.json")`.
This returns a dictionary keyed on asset origin, spanning every domain that
provides such a file. Our entry is merged into the same pool as base-game
story structures.

**Placement rationale.**

- `±2500 X, ±2500 Z` band overlaps Tortuga's 500–3000 radial range — a
  player exploring the surrounding region will plausibly encounter both.
- `requireLandform: "veryflat"` + `landformRadius: 80` biases placement
  toward plains / plateaus. The 27×23 footprint fits inside an 80-block
  radius with room to spare.
- `schematicYOffset: -1` sinks the base of the structure 1 block into grass
  for a natural seat (the potato king's house is a small ground-level
  building, not a tower).
- `generateGrass: true` — the schematic likely has `soil-*-none` tiles that
  benefit from procedural grass after placement.
- `skipGenerationCategories` prevents trees / shrubs / nearby structures /
  hotsprings / patches from intruding on the placed schematic (same pattern
  base-game story structures use).
- `group: "storystructure"` — matches base-game convention. The group is
  used only as a metadata tag on `GeneratedStructure` records; it does not
  couple the structure to base-game "story mode" progression.

### 2. Update `map-potato.json`

Exactly one field changes:

```diff
 "locatorPropsbyType": {
     "*": {
-        "schematiccode": "buriedtreasurechest",
+        "schematiccode": "potato-king-house",
         "waypointtext": "location-potato",
         "waypointicon": "x",
         "waypointcolor": [0.6, 0.4, 0.1, 1]
     }
 }
```

**Why `"potato-king-house"` matches.** `GenStoryStructures` stores
`GeneratedStructure.Code = structure.Code + ":" + structure.Schematics[0]`,
so our structure shows up as `"potatoking:surface/potato-king-house"`.
`FindFreshStructureLocation` does `Code.Split('/')` and matches against
`parts[1]`, which is `"potato-king-house"`.

No class change. `map-potato` continues using base-game `ItemLocatorMap`
with the default 350-block search range. A player at spawn holding the
map gets "No location found" until they're within 350 of the house — same
behavior as base-game locator maps. This matches the earlier directive
that raised ocean-map search range to 2000 *only* for Seafarer's
`ItemOceanLocatorMap` users (crimsonrose, tortuga).

## Scope excluded (YAGNI)

- No changes to `ItemLocatorMap` / `ItemOceanLocatorMap`
- No changes to Celeste's dialogue (existing map delivery path unchanged)
- No `rocktypeRemapGroups` for this structure — fixed block palette
- No lang changes (`location-potato`, `item-map-potato`, `itemdesc-map-potato`
  already exist in `en.json`)
- No `buildProtected` / build-claim regions — this is discoverable content,
  not a sacred site
- No `forceTemperature` / `forceRain` overrides
- Potato-king-house is *not* given the Tortuga-style singleton tracking in
  `GenOceanStructures`; the story-structures system handles singleton + save
  persistence independently

## Known tensions / verification points

**Cross-domain schematic resolution.** Our config at
`seafarer:worldgen/storystructures.json` declares
`schematics: ["surface/potato-king-house"]` (no domain prefix). Story
structures resolve schematic paths against the config's asset origin, so
this should find `seafarer:worldgen/surface/potato-king-house.json`. If it
fails (server log: "schematic not found"), fall back to explicit prefix:
`"schematics": ["seafarer:surface/potato-king-house"]`.

**Landform availability.** `"veryflat"` is a base-game landform code; check
runtime logs for "landform not found" and fall back to `"flat"` or similar
if the code differs in 1.22.

**Placement failure mode.** If no matching landform exists within the
±2500 band, the structure fails to place and the game surfaces a
`FailedToGenerateLocation` warning to admins on login. This is the
base-game safety net; no special handling needed from our side.

## Files changed / created

| File | Change |
|---|---|
| `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json` | Create — new structure entry |
| `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json` | Modify — update `schematiccode` |

## Testing plan

1. **Build**: `dotnet build Seafarer/Seafarer/Seafarer.csproj` — succeeds.
2. **Asset validation**: `python3 validate-assets.py` — no new errors.
3. **Fresh world creation**: create a new world; on first player login,
   check the server log for either successful placement or
   `FailedToGenerateLocation`. If success, `/wgen storystruc tp potatoking`
   (or `/tpstoryloc potatoking`) teleports to the placed house.
4. **Map behavior — far from house**: at spawn, right-click `map-potato`.
   Expected: "No location found on this map."
5. **Map behavior — near house**: teleport to within 300 blocks of the
   placed structure, right-click `map-potato`. Expected: waypoint added
   at the house's approximate location.
6. **Regression — Celeste dialogue**: step through Celeste's quest that
   gifts `map-potato` (no dialogue changes, so this should behave
   identically). Confirm the inventory item is still `seafarer:map-potato`.
