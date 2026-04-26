# Outrigger Boat — Design Spec

**Date:** 2026-04-26
**Mod:** Seafarer
**Status:** Approved (pending implementation plan)

## Overview

Add a new sailed boat — the outrigger canoe — to the Seafarer mod. The outrigger is a fast, lightweight, twin-hulled fishing boat inspired by Filipino bangka and Asian-Pacific fishing canoes. It fills a niche between the slow, high-cargo log barge and the heavy, balanced sailed boat: faster than both, with modest cargo capacity, and built with full shipwright-style construction stages.

The outrigger schematic is already a quest reward from Drake (`drake-seasoned` quest — deliver 160 seasoned planks) and Drake also resells it. The schematic itself remains in the player's inventory after use (`noConsumeOnCrafting: true`) — it's a permanent blueprint.

## Goals

- Reuse the **base game** boat-construction system (`Vintagestory.GameContent.EntityBoatConstruction`) via a Harmony prefix patch on its private `Spawn` method, allowing arbitrary boat types to plug in by declaring an `attributes.boattype` discriminator on the construction entity.
- No hard dependency on the Shipwright mod.
- Use the existing `schematic-outrigger` item as a permanent crafting ingredient.
- Support the full base-game wood matrix plus Seafarer's `seasoned` and `varnished` plank finishes.
- Multi-stage construction (10 stages) so the build feels like a real shipyard project, matching the scale of the 160-plank quest gate.

## Non-Goals

- No fishing-rod-mountable wearable category. Cargo slots take chests/baskets/storage vessels/crates only.
- No new training profession or `requiredLevel` gate. Schematic is the only gate.
- No worldgen loot for the schematic. Acquisition is exclusively the Drake quest + Drake's tradelist.
- No writing surface, ratlines, plaques, shields, or figurehead slots (those belong to the heavier sailed boat).
- No optional Shipwright integration / dual-pathway. Self-contained mod.

## Architecture

Base game's `EntityBoatConstruction.Spawn()` is `private` (not virtual) and is invoked from inside `OnGameTick()`. A subclass cannot override `Spawn()`, and overriding `OnGameTick()` cannot prevent the base's `Spawn()` from running once `base.OnGameTick(dt)` is called. The cleanest hook is therefore a Harmony prefix patch — Seafarer already uses Harmony (`HarmonyPatches/FruitTreeDwarfPatch.cs`).

**One Harmony patch + one rollers subclass + two stub C# classes:**

| Element | Type | Purpose |
|---|---|---|
| `EntityBoatConstructionSpawnPatch` | Harmony prefix on `Vintagestory.GameContent.EntityBoatConstruction.Spawn` | Reads `boattype` from the construction entity's `Properties.Attributes` (defaulting to `"sailed"`). For non-default boat types, spawns `boat-{boattype}-{wood}` with the same offset / motion / `createdBy*` attributes the original would, then returns `false` to skip the original method. For `"sailed"`, returns `true` and the original runs unchanged. The patch reads via reflection: the wood wildcard from the protected `rcc` field (`RightClickConstruction.StoredWildCards["wood"]`), the private `launchingEntity` field, and the protected `launchStartPos` field — all via Harmony's `AccessTools.Field`. |
| `ItemOutriggerRollers` | Subclass of `Vintagestory.GameContent.ItemRoller` | Overrides `OnHeldInteractStart` to spawn the `outrigger-construction-oak` entity instead of the base-game-hardcoded `boatconstruction-sailed-oak`. Reuses the parent's suitable-location/orient checks via `siteListByFacing` etc. Material defaults to `oak` for the construction entity name; the actual boat material is determined later by the keel-stage plank wildcard. |
| `EntityOutriggerBoat` | Subclass of `Vintagestory.GameContent.EntityBoat` | Stub. No overrides initially. Reserved namespace for future fishing-tuned behavior (custom speed in current, fishing-rod interactions, etc.). |
| `ItemOutriggerBoat` | Subclass of `Vintagestory.GameContent.ItemBoat` | Stub. Reserved namespace for future custom interactions. |

The three concrete classes are registered in `SeafarerModSystem.Start()` via `api.RegisterEntity()` / `api.RegisterItemClass()`. The Harmony patch is applied during the same Start hook (an explicit `harmony.Patch` call for this one method).

The construction-side entity (`outrigger-construction.json`) uses `class: "EntityBoatConstruction"` directly — the base game class — and declares `attributes.boattype: "outrigger"` in JSON, mirroring the way shipwright's catboat-construction declares `boattype: "catboat"`. The Harmony prefix is what teaches the otherwise-hardcoded base-game spawn how to read that attribute.

This pattern is reusable: any future Seafarer boat can plug into the same patch by declaring its own `boattype` value and providing a matching `boat-{type}-{material}` entity.

## Files

### New C# files

```
Seafarer/Seafarer/HarmonyPatches/EntityBoatConstructionSpawnPatch.cs
Seafarer/Seafarer/Entity/EntityOutriggerBoat.cs
Seafarer/Seafarer/Item/ItemOutriggerBoat.cs
Seafarer/Seafarer/Item/ItemOutriggerRollers.cs
```

### Modified C# files

```
Seafarer/Seafarer/SeafarerModSystem.cs   // register the three classes and apply the Harmony patch
```

### New JSON assets (under `Seafarer/Seafarer/assets/seafarer/`)

```
entities/nonliving/boat-outrigger.json          // launched, ridable boat (class: EntityOutriggerBoat)
entities/nonliving/outrigger-construction.json  // in-progress build with stages (class: EntityBoatConstruction; attributes.boattype: "outrigger")
itemtypes/boats/boat-outrigger.json             // placeable boat item (class: ItemOutriggerBoat)
itemtypes/boats/boat-outrigger-rollers.json     // starter rollers item (class: ItemOutriggerRollers; placed to spawn construction)
recipes/grid/outrigger-rollers.json             // schematic + firewood + rope → rollers
shapes/entity/nonliving/boat/boat-outrigger.json          // finished boat shape (artist work)
shapes/entity/nonliving/boat/outrigger-construction.json  // construction shape (artist work)
```

### Modified existing assets

```
lang/en.json   // ~15 new entries (see Language section)
```

### Skipped / non-changes

- No worldgen patches.
- No JSON patches to base game files (the `block/wood` worldproperty is not modified — `seasoned`/`varnished` are added as explicit `states` in the variantgroup).
- No `assets/seafarer/config/training/` changes.

## Variant Scope

Both the boat entity, the construction entity, and the boat item share the same `material` variantgroup:

```jsonc
variantgroups: [
    { code: "type", states: ["outrigger"] },
    { code: "material", states: ["seasoned", "varnished"], loadFromProperties: "block/wood" }
],
skipVariants: [
    "*-aged",
    "*-veryaged",
    "*-rotten",
    "*-veryrotten"
]
```

This yields ~14 shipping variants: 12 wood species (oak, birch, maple, pine, acacia, kapok, baldcypress, larch, redwood, walnut, ebony, purpleheart) plus `seasoned` and `varnished`. The `aged` / `veryaged` / `rotten` / `veryrotten` flag-states from `block/wood` are skipped because they don't fit the "fresh, finished fishing boat" theme — those finishes belong to the heritage-feel sailed boat.

`seasoned` and `varnished` planks (`game:plank-seasoned`, `game:plank-varnished`, `game:supportbeam-seasoned`, `game:supportbeam-varnished`) already exist as items because Seafarer's `patches/plank-variants.json` adds them as states to the relevant base-game items. No new patches needed.

## Construction Stages

10 build stages plus stage 0 (initial rollers) and stage 11 (cleanup remove-props). The construction entity carries them in its `attributes.stages` array, processed by base-game `EntityBoatConstruction` / `RightClickConstruction`. The construction entity itself uses base-game `class: "EntityBoatConstruction"` plus an `attributes.boattype: "outrigger"` discriminator that the Harmony patch reads at launch time.

| # | Stage | `requireStacks` | `addElements` |
|---|---|---|---|
| 0 | Rollers placed | (initial) | `ORIGIN/ORIGIN0rollers` |
| 1 | Keel | 8× `firewood`, 12× `plank-*` (storeWildCard `wood`, name `seafarer-outrigger-ingredient-planks`) | `ORIGIN/ORIGINBoat/ORIGIN1Keel`, `ORIGIN/ORIGIN1props` |
| 2 | Ribs / spines | 10× `supportbeam-{wood}` | `ORIGIN/ORIGINBoat/ORIGIN2Ribs` |
| 3 | Hull planking (lower) | 16× `plank-{wood}` | `ORIGIN/ORIGINBoat/ORIGIN3HullLower` |
| 4 | Hull planking (upper) | 16× `plank-{wood}` | `ORIGIN/ORIGINBoat/ORIGIN4HullUpper` |
| 5 | Outrigger floats (port + starboard) | 12× `supportbeam-{wood}` | `ORIGIN/ORIGINBoat/ORIGIN5Floats` |
| 6 | Outrigger crossbeams | 8× `supportbeam-{wood}`, 6× `rope` | `ORIGIN/ORIGINBoat/ORIGIN6Crossbeams` |
| 7 | Mast | 8× `supportbeam-{wood}` | `ORIGIN/ORIGINBoat/ORIGIN7Mast` |
| 8 | Rigging | 12× `rope` | `ORIGIN/ORIGINBoat/ORIGIN8Rigging` |
| 9 | Sail | 16× `linen-normal-down`, 4× `rope` | `ORIGIN/ORIGINBoat/ORIGIN9Sail` |
| 10 | Launch | (no materials) `actionLangCode: "outrigger-launch"` | (triggers `launch` animation) |
| 11 | Cleanup | — | `removeElements: ["ORIGIN/ORIGIN1props"]` |

**Keel-stage wood capture** uses `code: "plank-*"` and `storeWildCard: "wood"`. This differs from the base-game / Shipwright catboat which captures from `log-placed-*`. Capturing from planks is required to support `seasoned` / `varnished` finishes (which only exist as planks, not as logs). The captured `{wood}` value substitutes into all subsequent stages' material codes.

**Stage 0 deconstruct**: while in stage 0 with an empty hand and sneak held, right-click refunds 5× base-game `roller` items and removes the construction entity. This is base-game `EntityBoatConstruction.OnInteract` behavior, hardcoded to the base-game `roller` item code — since we are not subclassing the construction entity, we accept the refund as-is. Trade-off: a player who deconstructs a stage-0 outrigger build gets generic rollers back, not a Seafarer-specific item. They can recraft `boat-outrigger-rollers` from those generic rollers using the `boat-outrigger-rollers` recipe (see Starter Mechanic below) — net cost is the firewood + rope they originally added, plus they keep the schematic. Not exploitable: any sailed-boat construction made from those refunded rollers still requires the player to gather the full sailed-boat material set. **If we later want Seafarer-specific refunds we can add a second Harmony patch on `OnInteract`; flagged as a follow-up, not in initial scope.**

**Total construction cost**: 44 planks + 38 supportbeams + 22 rope + 16 linen + 8 firewood. Roughly 30% of the Shipwright catboat's material cost — appropriate for a "small fast fishing boat."

## Starter Mechanic — Rollers Item

The construction entity is summoned by placing a `boat-outrigger-rollers` item (a Seafarer-specific item using class `ItemOutriggerRollers : ItemRoller`).

**Item asset** (`itemtypes/boats/boat-outrigger-rollers.json`):
- `class: "ItemOutriggerRollers"`
- `maxstacksize: 5` (matches base game roller stack)
- No material variants — single item.
- `creativeinventory: { "general": ["*"], "items": ["*"], "seafarer": ["*"] }`
- Same `combustibleProps`, `materialDensity`, transforms as base game roller.
- Shape: reuse base game `item/roller` shape.

**Grid recipe** (`recipes/grid/outrigger-rollers.json`):
- Pattern: shapeless — 1 schematic + 5 firewood + 4 rope.
- Ingredients:
  - `seafarer:schematic-outrigger` × 1, with `isTool: true` (or equivalent — schematic must NOT be consumed; it's a permanent blueprint per the user's design intent)
  - `game:firewood` × 5
  - `game:rope` × 4
- Output: `seafarer:boat-outrigger-rollers` × 5 (matches the 5-stack the placement check expects)

**Schematic non-consumption**: the schematic item itself has `attributes.noConsumeOnCrafting: true` (already present in the existing `schematic-outrigger.json`). The grid recipe must reference the schematic as an ingredient that respects that attribute — verified during implementation by checking the recipe consumption mechanic against an existing Seafarer recipe that uses noConsumeOnCrafting items, or by adding `recipeAttributes: { noConsume: true }` if needed.

**Rollers item placement**: right-clicking the rollers item on a valid surface (water-adjacent, like base-game rollers) spawns the `outrigger-construction-oak` entity in stage 0. The "oak" suffix is the construction entity's default material variant (used for the initial element textures before the keel-stage wildcard is captured). The actual launched boat's material is determined by the keel-stage plank wildcard, not by this default.

## Launched-Boat Mechanics

`boat-outrigger-{material}` entity attributes:

```jsonc
{
    code: "boat",
    class: "EntityOutriggerBoat",
    tags: ["inanimate", "vehicle"],
    weight: 800,                       // sailed=1900, log barge=200
    attributes: {
        disabledweatherVaneAnimCode: "weathervane",
        deconstructible: true,
        deconstructDrops: [
            { type: "item", code: "game:plank-{material}", quantity: 32 },
            { type: "block", code: "game:supportbeam-{material}", quantity: 24 },
            { type: "item", code: "game:rope", quantity: 12 },
            { type: "block", code: "game:linen-normal-down", quantity: 8 }
        ],
        shouldSwivelFromMotion: false,
        speedMultiplier: 1.4,          // sailed=1.2, log barge=0.8
        swimmingOffsetY: 0.7,
        unfurlSails: true,
        mountAnimations: { idle: "sitboatidle", ready: "", forwards: "", backwards: "" }
    },
    hitboxSize: { x: 4, y: 1.0, z: 3 }
}
```

**Collision boxes** (3 total — main hull + 2 outrigger floats):

```jsonc
"passivephysicsmultibox": {
    collisionBoxes: [
        { x1: -0.5, y1: 0, z1: -2.5, x2:  0.5, y2: 1.0, z2:  2.5 }, // main hull
        { x1: -2.0, y1: 0, z1: -1.5, x2: -1.4, y2: 0.4, z2:  1.5 }, // port float
        { x1:  1.4, y1: 0, z1: -1.5, x2:  2.0, y2: 0.4, z2:  1.5 }  // starboard float
    ],
    groundDragFactor: 1,
    airDragFallingFactor: 0.5,
    gravityFactor: 1.0
}
```

**Ellipsoidal repulse**: `radius: { x: 2.5, y: 1.5, z: 3.0 }` (wider than sailed boat because of the outrigger float span).

**Seats** (2, both controllable):

```jsonc
"creaturecarrier": {
    seats: [
        { apName: "ForeSeatAP", controllable: true, mountOffset: { x: 0, z:  1.4 }, bodyYawLimit: 0.4, eyeHeight: 1 },
        { apName: "AftSeatAP",  controllable: true, mountOffset: { x: 0, z: -1.4 }, bodyYawLimit: 0.4, eyeHeight: 1 }
    ]
}
```

**Accessory slots** via `rideableaccessories`:

| Slot code | `forCategoryCodes` | Attachment point |
|---|---|---|
| `Oar Storage` | `["oar"]` | `OarAP` → `OarStorage` element |
| `Sail Recolor` | `["sailrecolor"]` | `SailExtraStorageAP` |
| `Mast Lantern` | `["lantern"]` | `MastLanternAP` → `MastLantern` element |
| `Cargo Fore` | `["chest","basket","storagevessel","crate"]` | `CargoForeAP` → `CargoFore`, behindSlots: `["Cargo Mid"]` |
| `Cargo Mid` | `["chest","basket","storagevessel","crate"]` | `CargoMidAP` → `CargoMid`, behindSlots: `["Cargo Aft"]` |
| `Cargo Aft` | `["chest","basket","storagevessel","crate"]` | `CargoAftAP` → `CargoAft` |

**Selection boxes**: all attachment-point codes listed above plus `ForeSeatAP` and `AftSeatAP`.

**Behaviors**:
- Client: `ellipsoidalrepulseagents`, `passivephysicsmultibox`, `interpolateposition`, `hidewatersurface` (with `hideWaterElement: "ORIGIN/hideWater/*"`), `selectionboxes`, `rideableaccessories`, `creaturecarrier`
- Server: `ellipsoidalrepulseagents`, `passivephysicsmultibox`, `selectionboxes`, `rideableaccessories`, `creaturecarrier`

No `writingsurface` (no plaques), no shield/figurehead/ratlines slots.

**Animations**: `turnLeft`, `turnRight`, `weathervane` — all standard.

**Textures** (mirroring catboat pattern):

```jsonc
texturesByType: {
    "*": {
        "material":   { base: "game:block/wood/debarked/{material}" },
        "wood":       { base: "game:block/wood/debarked/{material}" },
        "planks":     { base: "game:block/wood/planks/{material}1" },
        "plain":      { base: "game:block/cloth/linen/plain" },
        "rope":       { base: "game:item/resource/rope" },
        "transparent":{ base: "game:block/transparent" }
    }
}
```

(For `material = seasoned` / `varnished`, the existing Seafarer texture patches resolve `{material}1` correctly via the wildcard match in plank texture lookup.)

## Item Side — `boat-outrigger.json`

```jsonc
{
    code: "boat",
    class: "ItemOutriggerBoat",
    variantgroups: [
        { code: "type", states: ["outrigger"] },
        { code: "material", states: ["seasoned", "varnished"], loadFromProperties: "block/wood" }
    ],
    skipVariants: ["*-aged", "*-veryaged", "*-rotten", "*-veryrotten"],
    shapeByType: {
        "boat-outrigger-*": { base: "entity/nonliving/boat/boat-outrigger", ignoreElements: ["hideWater"] }
    },
    texturesByType: {
        "*": {
            "material":   { base: "game:block/wood/debarked/{material}" },
            "wood":       { base: "game:block/wood/debarked/{material}" },
            "planks":     { base: "game:block/wood/planks/{material}1" },
            "plain":      { base: "game:block/cloth/linen/plain" },
            "rope":       { base: "game:item/resource/rope" },
            "transparent":{ base: "game:block/transparent" }
        }
    },
    attributes: {
        handbook: {
            groupBy: ["boat-{type}-*"],
            extraSections: [
                { title: "handbook-seafarer-outrigger-craftinfo", text: "handbook-seafarer-outrigger-craftinfo" }
            ]
        }
    },
    heldTpIdleAnimation: "boatholdoverhead",
    maxstacksize: 1,
    materialDensity: 1,
    liquidSelectable: 1,
    creativeinventory: { "general": ["*"], "items": ["*"], "tools": ["*"], "seafarer": ["*"] },
    guiTransform: { /* tuned during artist pass */ },
    groundTransform: { /* tuned, ~scale 3.0 */ },
    tpHandTransform: { /* tuned */ }
}
```

## Language Entries

Added to `assets/seafarer/lang/en.json`:

```jsonc
{
    "item-boat-outrigger-*": "Outrigger Canoe",
    "entity-boat-outrigger-*": "Outrigger",
    "entity-outrigger-construction": "Outrigger (under construction)",
    "itemdesc-boat-outrigger": "A fast, lightweight twin-hulled fishing boat. Two seats and a single sail.",
    "item-boat-outrigger-rollers": "Outrigger Building Rollers",
    "itemdesc-boat-outrigger-rollers": "Place on water-adjacent ground to begin building an outrigger.",

    "outrigger-launch": "Launch the outrigger",

    "outrigger-stage-1": "Lay the keel (firewood + planks)",
    "outrigger-stage-2": "Install the ribs",
    "outrigger-stage-3": "Plank the lower hull",
    "outrigger-stage-4": "Plank the upper hull",
    "outrigger-stage-5": "Attach the outrigger floats",
    "outrigger-stage-6": "Lash the outrigger crossbeams",
    "outrigger-stage-7": "Raise the mast",
    "outrigger-stage-8": "Rig the lines",
    "outrigger-stage-9": "Hoist the sail",

    "handbook-seafarer-outrigger-craftinfo": "The outrigger schematic is a quest reward from Drake (deliver 160 seasoned planks). Drake also resells the schematic. Craft the schematic with rope and firewood to get rollers, then place the rollers near water to begin construction."
}
```

## Build & Validation

- `python3 validate-assets.py` from repo root — must exit 0 with 0 errors before commit. Covers JSON parsing, lang key references, texture/shape file references, naming conventions.
- `dotnet build Seafarer/Seafarer.csproj` — must compile cleanly with the three new classes (`EntityOutriggerBoat`, `ItemOutriggerBoat`, `ItemOutriggerRollers`) registered in `SeafarerModSystem.Start()` and the Harmony patch (`EntityBoatConstructionSpawnPatch`) applied via `harmony.Patch`.
- Manual smoke test:
    1. Start dev VS with the mod loaded.
    2. Creative inventory → place a `boat-outrigger-rollers` item near water → confirm `outrigger-construction-{material}` entity spawns.
    3. Walk through stages 1–9 with creative materials → confirm each stage's elements appear and `requireStacks` consume correctly.
    4. Stage 10 launch → confirm `boat-outrigger-{material}` entity spawns with the right offset and the construction entity disappears.
    5. Mount, sail, attach a basket and a lantern → confirm slots and seats work.
    6. Right-click empty-hand-sneak on stage-0 construction → confirm rollers refund.

## Open Questions

- **Shape art**: both shape JSONs (`boat-outrigger.json` and `outrigger-construction.json`) require artist work. The element naming hierarchy in this spec (`ORIGIN/ORIGINBoat/ORIGIN{N}{StageName}`, `hideWater`, attachment points listed in the slots table) is the contract the C# code and stage definitions assume. The artist must implement to that contract; otherwise the construction stages and boat behaviors will not function. The construction shape additionally needs a `launch` animation and a `Center` attachment point (used by base-game `EntityBoatConstruction.getCenterPos`).
- **Schematic-as-ingredient mechanic**: the recipe must reference `seafarer:schematic-outrigger` as an ingredient that respects its `noConsumeOnCrafting: true` attribute. Verify during implementation by examining whether grid recipes in Vintage Story honor that attribute automatically (they should — same mechanic the base game uses for `tool-axe` style ingredients), or whether a `recipeAttributes` flag must be added.
