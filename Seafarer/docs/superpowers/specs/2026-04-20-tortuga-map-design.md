# Tortuga Map — Design

**Date:** 2026-04-20
**Status:** Approved

## Summary

Add `seafarer:map-tortuga`, a "message in a bottle" item that marks the
location of the world-singleton Tortuga port hub on the player's waypoint map.
Obtained via fishing saltwater/reef fish or panning sand/gravel.

Reuses the existing `ItemOceanLocatorMap` class (currently powering
`map-crimsonrose`) with one small extension to support a larger
structure-search range.

## Motivation

Tortuga is a singleton port hub that spawns 500–3000 blocks from world spawn.
Without a map, discovery is pure luck. A "message in a bottle" found through
ocean activities (fishing, panning wet sand) is thematically consistent and
gives players a gameplay reason to engage with those systems before they have
a reliable way to locate Tortuga.

Wreck drops were considered but deferred — will be added in a separate change.

## Design

### 1. Item definition (`assets/seafarer/itemtypes/lore/map-tortuga.json`)

```json5
{
  "code": "map-tortuga",
  "class": "ItemOceanLocatorMap",
  "maxstacksize": 1,
  "attributes": {
    "displaycaseable": true,
    "shelvable": true,
    "readable": true,
    "editable": false,
    "maxPageCount": 1,
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
  "creativeinventory": { "general": ["*"], "items": ["*"], "seafarer": ["*"] }
}
```

No new textures or shape — the `bottlemessage` shape already defines its own
texture fallbacks (`smoky glass`, `wood-generic`, `scroll`). Transforms
(gui/ground/tp) are omitted so VS defaults apply; if the held/inventory
rendering looks off, tune them post-implementation (matches the iterative
approach used for `map-crimsonrose`).

**Waypoint style:** amber/gold color (`[0.95, 0.75, 0.2, 1]`) to distinguish
from the red crimson-rose waypoint. Icon `x` matches existing convention.
`randomX/Z: 15` means the waypoint lands within a ~15-block radius of the
structure center, not pixel-perfect.

### 2. Code change (`Seafarer/Item/ItemOceanLocatorMap.cs`)

The current class hardcodes `FindFreshStructureLocation(..., 350)`. For a
singleton Tortuga 500–3000 blocks from spawn, 350 is too restrictive — a
player opening the bottle at spawn would always get "no location found."

**Change:** read an optional `searchRange` int from `Attributes`, default 350:

```csharp
// In OnLoaded:
searchRange = Attributes["searchRange"].AsInt(350);

// In OnHeldInteractStart (where 350 is currently hardcoded):
var loc = strucLocSys.FindFreshStructureLocation(props.SchematicCode, byEntity.Pos.AsBlockPos, searchRange);
```

This keeps `map-crimsonrose` (and `map-potato`) behavior identical (they don't
set the attribute → default 350). `map-tortuga` sets `searchRange: 10000` so
the bottle can find the singleton from anywhere in the world.

### 3. Fishing acquisition

JSON patch `assets/seafarer/patches/tortuga-map-fishing.json`:

```json5
[
  {
    "op": "add",
    "file": "game:entity/animal/fish/saltwater-adult.json",
    "path": "/drops/-",
    "value": { "type": "item", "code": "seafarer:map-tortuga", "chance": { "avg": 0.015, "var": 0 } }
  },
  {
    "op": "add",
    "file": "game:entity/animal/fish/reef-adult.json",
    "path": "/drops/-",
    "value": { "type": "item", "code": "seafarer:map-tortuga", "chance": { "avg": 0.015, "var": 0 } }
  }
]
```

Rate: ~1.5% per caught saltwater or reef fish. Freshwater fish are excluded —
thematically a bottle washed in from the sea.

### 4. Panning acquisition

JSON patch `assets/seafarer/patches/tortuga-map-panning.json`:

```json5
[
  {
    "op": "add",
    "file": "game:block/pan.json",
    "path": "/attributes/panningDrops/@(sand|gravel|sandwavy)-.*/-",
    "value": { "type": "item", "code": "seafarer:map-tortuga", "chance": { "avg": 0.0015, "var": 0 }, "manMade": true }
  }
]
```

Rate: ~0.15% per pan operation on sand/gravel. `manMade: true` matches
convention for non-geological drops in the pan table.

### 5. Lang entries (`assets/seafarer/lang/en.json`)

```json
{
  "item-map-tortuga": "Message in a Bottle — Tortuga",
  "location-tortuga": "Tortuga",
  "seafarer:itemdesc-map-tortuga": "A weathered scroll tucked inside a sea-tumbled bottle, its ink speaking of a far-off port called Tortuga."
}
```

## Scope excluded (YAGNI)

- **Wreck drops** — deferred to a separate feature
- **Custom shape/textures** — reuse `bottlemessage` as-is
- **Crafting recipe** — not craftable by design; players find them
- **Trader integration** — not sold
- **Multiple map variants** — one map per structure is enough
- **Map "consume on use" mechanic** — the existing class doesn't consume, so neither do we

## Files changed / created

| File | Change |
|---|---|
| `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json` | Create — new item definition |
| `Seafarer/Seafarer/Item/ItemOceanLocatorMap.cs` | Modify — add optional `searchRange` attribute |
| `Seafarer/Seafarer/assets/seafarer/patches/tortuga-map-fishing.json` | Create — append bottle to fish drops |
| `Seafarer/Seafarer/assets/seafarer/patches/tortuga-map-panning.json` | Create — append bottle to pan drops |
| `Seafarer/Seafarer/assets/seafarer/lang/en.json` | Modify — add item name, waypoint label, description |

## Testing plan

1. **Build**: `dotnet build Seafarer/Seafarer/Seafarer.csproj` — succeeds.
2. **Asset validator**: `python3 validate-assets.py` — no new errors.
3. **Creative inventory**: `map-tortuga` appears in the seafarer creative tab;
   its shape renders as a bottle with scroll inside.
4. **Unused map (no Tortuga nearby)**: in creative, spawn at world origin with
   Tortuga far away. Right-click the map → expected: waypoint added pointing
   at Tortuga's approximate location (because `searchRange: 10000` finds it).
5. **Used map (already pointed)**: right-click again → expected: "Location
   already marked on your map" message.
6. **Unchanged behavior — crimson rose**: `map-crimsonrose` still uses the
   default 350 search range (no `searchRange` attribute set on it). Verify by
   spawning one far from any wreck → "No location found" message, as before.
7. **Fishing drop**: catch saltwater/reef fish repeatedly in creative; bottle
   drop occasionally appears alongside fishraw (target: ~1 per 60–70 catches
   at 1.5%).
8. **Panning drop**: pan sand/gravel repeatedly; bottle drop is rare but
   present (target: ~1 per 600–700 pans at 0.15%).

Fishing/panning rate testing can be abbreviated to "confirm the drop appears
at least once in ~5 minutes of spammed activity" — precise statistics are not
needed for sign-off.
