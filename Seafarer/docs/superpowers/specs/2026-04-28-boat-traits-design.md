# Boat Traits Design

**Date:** 2026-04-28
**Status:** Approved

## Goal

Give boats per-instance traits sourced from sail material (canvas / oiled / waxed) and hull material (seasoned / varnished). Traits provide stat modifiers (HP, max HP, speed, storm damage taken) and surface in the look-at info with rarity color coding. Sail traits are applied in-world by right-clicking the cloth resource on the boat. Material traits are assigned at first spawn from the entity's variant. The system is JSON-driven so adding a new trait is a config + lang change.

## Scope

In scope:

- Two trait sources per boat: at most one `material` trait and one `sail` trait.
- Five named traits: Canvas Sail, Oiled Sail, Waxed Sail, Seasoned Hull, Varnished Hull.
- Modifiers: additive HP / max HP, additive speed bonus (% of base), multiplicative storm damage scale.
- Right-click apply for sail traits, gated by `hasSailSlot`. Replacing a sail drops the old cloth back.
- Material trait set once at first spawn from the entity's variant code.
- Look-at display: hull bar plus one line per trait, colored by rarity.
- Persistence via WatchedAttributes (auto save/load + network sync).

Out of scope:

- New trait sources beyond sail/material (oars, hull armor, figureheads). Could be added later by extending the source enum.
- In-game trait removal without replacement. Sails can only be swapped, not stripped.
- Trait UI beyond look-at info (no dedicated GUI).
- Re-applying a material trait after spawn (player can't re-coat an existing boat as varnished). Material is fixed by the entity's variant.

## Architecture

Two new code files, edits to one existing behavior, one new JSON registry, three item patches, and entity JSON updates.

### `BoatTrait` (data model + registry)

`Seafarer/Seafarer/EntityBehavior/BoatTrait.cs`. Three pieces:

- `enum TraitRarity { Common, Uncommon, Rare, Epic, Legendary }` with a `ColorHex` extension method returning the rarity hex.
- `class BoatTrait` — record-shaped: `Code`, `Source`, `Rarity`, `SpeedBonus`, `HealthBonus`, `StormDamageScale`. Default values: SpeedBonus=0, HealthBonus=0, StormDamageScale=1.
- `static class BoatTraitRegistry` — loaded once in `SeafarerModSystem.AssetsFinalize` from `seafarer:config/boat-traits.json`. Exposes `Get(string code) → BoatTrait?`.

### `EntityBehaviorShipMechanics` (extended)

Adds:

- `MaterialTraitCode` resolution from `extendShipMechanics.materialTraits` map at first spawn.
- `ApplyTrait(string source, string code)` and `RemoveTrait(string source) → string?` (returns the previously installed code so the caller can drop it back).
- `RecomputeTraitEffects()` — recomputes BaseMaxHealth, the `walkspeed` stat modifier, and the cached storm-damage scale used by `ApplyStormDamage`.
- `GetInfoText` extended: after the existing hull line, emit one trait line per active trait, colored by rarity.

### `BehaviorBoatSail` (item-side)

`Seafarer/Seafarer/CollectibleBehavior/BehaviorBoatSail.cs`. CollectibleBehavior. Reads two JSON properties on `Initialize`: `trait` (the trait code to apply when used).

`OnHeldInteractStart` against an entity with `EntityBehaviorShipMechanics`:

1. If the boat's `extendShipMechanics.hasSailSlot` is false → chat message, block default.
2. Look up the `BoatTrait` via the registry. If missing → log warning, no-op.
3. Server-side: drop the previous sail item if any (mapping from old trait code back to its source item via the trait's `dropItem` field), apply the new trait, recompute effects, consume one of the held cloth, play `game:sounds/block/cloth`, emit chat confirmation.
4. Client-side: set `handHandling = PreventDefault`, `handling = PreventSubsequent`.

## Trait Registry Schema

`Seafarer/Seafarer/assets/seafarer/config/boat-traits.json`:

```json
{
  "traits": {
    "canvas-sail": {
      "source": "sail",
      "rarity": "common",
      "speedBonus": 0.05,
      "healthBonus": 10,
      "dropItem": "seafarer:canvas-sail"
    },
    "oiled-sail": {
      "source": "sail",
      "rarity": "uncommon",
      "speedBonus": 0.10,
      "healthBonus": 20,
      "dropItem": "seafarer:oiled-canvas-sail"
    },
    "waxed-sail": {
      "source": "sail",
      "rarity": "rare",
      "speedBonus": 0.15,
      "healthBonus": 30,
      "dropItem": "seafarer:waxed-canvas-sail"
    },
    "seasoned-hull": {
      "source": "material",
      "rarity": "uncommon",
      "healthBonus": 50
    },
    "varnished-hull": {
      "source": "material",
      "rarity": "rare",
      "healthBonus": 75,
      "stormDamageScale": 0.75
    }
  }
}
```

`dropItem` is optional; absent means no item drops back when the trait is removed (used for material traits, which can't be removed). For sail traits it must point at the source cloth item.

## Boat-Side Config

In each boat JSON's `extendShipMechanics` block, two new fields:

```json
{
  "extendShipMechanics": {
    "health": 200,
    "hasSailSlot": true,
    "materialTraits": {
      "seasoned": "seasoned-hull",
      "varnished": "varnished-hull"
    },
    "collision": { ... },
    "storm": { ... },
    "wreckage": { ... }
  }
}
```

`materialTraits` keys are matched against the entity variant's material code (e.g., `entity.Code` parts). For variants without a matching key, no material trait is applied.

Per-boat values:

| Boat | hasSailSlot | materialTraits |
|---|---|---|
| seafarer outrigger (varnished/seasoned) | true | `seasoned → seasoned-hull`, `varnished → varnished-hull` |
| seafarer logbarge (aged) | true | (none) |
| vanilla raft (aged/bamboo) | false | (none) |
| vanilla sailed | true | (none) |

## Storage Format

`entity.WatchedAttributes` → tree `shipTraits`:

```
shipTraits/
  material/
    code: "seasoned-hull"
  sail/
    code: "canvas-sail"
```

Both nested trees are optional. Reading a trait: read the `code` string, look up `BoatTraitRegistry.Get(code)` for the live values. The rarity/modifiers are not persisted — only the code — so balance changes propagate without migration.

## Effects

`RecomputeTraitEffects()` runs every time traits are mutated (and once during `AfterInitialized`). It iterates the active traits and:

1. **Health / Max Health**: `EntityBehaviorHealth.BaseMaxHealth = configuredHealth + sumOf(traits, t => t.HealthBonus)`. Call `UpdateMaxHealth()`. On a fresh apply (not load), bump `Health` by the delta so the player feels the upgrade. On removal of a sail trait that previously provided +HP, re-clamp `Health` to the new MaxHealth.
2. **Speed**: `entity.Stats.Set("walkspeed", "shipTraits", 1.0f + sumOf(traits, t => t.SpeedBonus), persistent: true)`. Vanilla `EntityBoat` already multiplies movement by the `walkspeed` stat, so this just works.
3. **Storm damage scale**: cache a single field `stormDamageMultiplier = product(traits, t => t.StormDamageScale)`. `ApplyStormDamage` multiplies its computed damage by this before subtracting from health.

Combat and collision damage are NOT scaled by traits — keeps player-attributed hits feeling honest.

## Application Flow

### Material (at spawn)

In `AfterInitialized(onFirstSpawn)`:

```text
1. read cfg.materialTraits (skip if absent)
2. parse entity.Code into variant parts
3. for each (key, traitCode) in materialTraits:
     if entity.Code matches key (e.g., "varnished" found in code parts):
       set shipTraits/material/code = traitCode
       break
4. RecomputeTraitEffects()
```

`onFirstSpawn=false` (i.e., entity loaded from disk): skip step 1-3 (already persisted), but still call `RecomputeTraitEffects()` so the speed stat is reapplied (stat modifiers don't persist in WatchedAttributes — only the trait code does).

### Sail (right-click cloth on boat)

In `BehaviorBoatSail.OnHeldInteractStart`:

```text
1. require entitySel.Entity to have EntityBehaviorShipMechanics → ship
2. if !ship.HasSailSlot → chat "seafarer:boat-no-sail-slot", PreventDefault, return
3. look up trait in registry → newTrait (skip with log if missing)
4. server-only:
     a. oldCode = ship.RemoveTrait("sail")
     b. if oldCode != null and oldTrait.dropItem set → spawn 1× dropItem at boat
     c. ship.ApplyTrait("sail", trait.code)
     d. ship.RecomputeTraitEffects() — also bumps Health by delta
     e. consume 1 from slot; play sound; chat "seafarer:sail-applied"
5. set handling = PreventSubsequent, handHandling = PreventDefault
```

## Look-At Display

`EntityBehaviorShipMechanics.GetInfoText` (extended):

```text
1. emit existing Hull: X/Y bar line (unchanged)
2. for source in [material, sail]:
     code = shipTraits/{source}/code (skip if null)
     trait = registry.Get(code) (skip with log if null)
     line = Lang.Get("seafarer:trait-line-{trait.rarity}",
                     Lang.Get("seafarer:trait-{code}"),
                     Lang.Get("seafarer:rarity-{trait.rarity}"))
     infotext.AppendLine(line)
```

Lang templates carry the color markup so the C# never hardcodes hex:

```json
"seafarer:trait-line-common":    "<font color=\"#9D9D9D\">■ {0} ({1})</font>",
"seafarer:trait-line-uncommon":  "<font color=\"#1EFF00\">■ {0} ({1})</font>",
"seafarer:trait-line-rare":      "<font color=\"#0070FF\">■ {0} ({1})</font>",
"seafarer:trait-line-epic":      "<font color=\"#A335EE\">■ {0} ({1})</font>",
"seafarer:trait-line-legendary": "<font color=\"#FF8000\">■ {0} ({1})</font>"
```

Resulting render (for a varnished outrigger with a waxed sail):

```
Hull: 245/280 ██████████████░░
■ Varnished Hull (Rare)
■ Waxed Sail (Rare)
```

## Lang Strings (new)

```json
"seafarer:trait-canvas-sail": "Canvas Sail",
"seafarer:trait-oiled-sail": "Oiled Sail",
"seafarer:trait-waxed-sail": "Waxed Sail",
"seafarer:trait-seasoned-hull": "Seasoned Hull",
"seafarer:trait-varnished-hull": "Varnished Hull",

"seafarer:rarity-common": "Common",
"seafarer:rarity-uncommon": "Uncommon",
"seafarer:rarity-rare": "Rare",
"seafarer:rarity-epic": "Epic",
"seafarer:rarity-legendary": "Legendary",

"seafarer:trait-line-common":    "<font color=\"#9D9D9D\">■ {0} ({1})</font>",
"seafarer:trait-line-uncommon":  "<font color=\"#1EFF00\">■ {0} ({1})</font>",
"seafarer:trait-line-rare":      "<font color=\"#0070FF\">■ {0} ({1})</font>",
"seafarer:trait-line-epic":      "<font color=\"#A335EE\">■ {0} ({1})</font>",
"seafarer:trait-line-legendary": "<font color=\"#FF8000\">■ {0} ({1})</font>",

"seafarer:boat-no-sail-slot": "This boat can't use a sail.",
"seafarer:sail-applied": "New sail rigged."
```

## Item Patches

`Seafarer/Seafarer/assets/seafarer/patches/`:

- `canvas-sail-boatsail.json` — `addmerge` `boatsail` behavior with `trait: "canvas-sail"` onto `seafarer:itemtypes/resource/canvas-sail.json`.
- `oiled-canvas-sail-boatsail.json` — same pattern, `trait: "oiled-sail"`.
- `waxed-canvas-sail-boatsail.json` — same pattern, `trait: "waxed-sail"`.

Each is a single `addmerge` op on `/behaviors`:

```json
[{ "op": "addmerge", "path": "/behaviors", "value": [{ "name": "boatsail", "trait": "canvas-sail" }],
   "file": "seafarer:itemtypes/resource/canvas-sail.json" }]
```

## Edge Cases

- **Trait code unknown to registry** (renamed/removed trait, old save): log warning once per occurrence, skip the trait line in look-at, ignore in effect calc. Save remains valid.
- **Material variant absent from `materialTraits`** (vanilla raft, bamboo): no material trait applied. Look-at shows only the hull bar line.
- **`hasSailSlot` missing from config**: defaults to `false`. Sail items can't be applied.
- **Sail behavior present on item but `trait` JSON property missing**: log warning; right-click does nothing.
- **`dropItem` resolves to a missing item code**: log warning, skip the drop, but still apply the new trait. Don't fail the upgrade because of asset issues.
- **Boat dies while wearing a sail**: existing wreckage drop unchanged. Sail item is NOT spawned separately. (Could revisit, but parts of a destroyed boat aren't supposed to be salvageable in full.)
- **Same sail re-applied to boat already wearing it**: drops the old (same), consumes the new (same), no net loss but wasteful — the player is responsible. Not worth special-casing.

## Testing Strategy

Manual playtest (no test framework). Per the existing pattern:

1. Spawn each of the four boat types. Look at each — verify hull line appears, no trait line for plain boats, correct material trait line for outrigger-seasoned/varnished.
2. Verify outrigger HP totals match (200 + 50 = 250 for seasoned, 280 + 75 = 355 for varnished).
3. Right-click canvas-sail on logbarge → trait line appears, MaxHP +10, speed bumped. Sail trait shows green color in look-at.
4. Right-click waxed-sail on the same logbarge → canvas drops back at the boat, waxed trait replaces, MaxHP delta applies.
5. Right-click canvas-sail on vanilla raft → fail message ("This boat can't use a sail."), no consume, no trait.
6. Save & quit → reload. All traits persist with correct effects.
7. Storm damage on varnished outrigger vs vanilla sailed → varnished takes ~75% damage per tick.
8. Damage a boat to half HP, repair it, verify the post-repair MaxHP matches the trait-modified MaxHP (not the base).

`validate-mod-assets` covers the JSON-only checks (recipes, lang entries, etc.).

## Files

New code:
- `Seafarer/Seafarer/EntityBehavior/BoatTrait.cs`
- `Seafarer/Seafarer/CollectibleBehavior/BehaviorBoatSail.cs`

New JSON:
- `Seafarer/Seafarer/assets/seafarer/config/boat-traits.json`
- `Seafarer/Seafarer/assets/seafarer/patches/canvas-sail-boatsail.json`
- `Seafarer/Seafarer/assets/seafarer/patches/oiled-canvas-sail-boatsail.json`
- `Seafarer/Seafarer/assets/seafarer/patches/waxed-canvas-sail-boatsail.json`

Modified:
- `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs` — material trait at spawn, trait apply/remove/recompute, extended GetInfoText.
- `Seafarer/Seafarer/SeafarerModSystem.cs` — register `boatsail`, load `BoatTraitRegistry`.
- `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-outrigger.json` — `hasSailSlot`, `materialTraits` per variant.
- `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-logbarge.json` — `hasSailSlot: true`.
- `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-raft-shipmechanics.json` — `hasSailSlot: false`.
- `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-sailed-shipmechanics.json` — `hasSailSlot: true`.
- `Seafarer/Seafarer/assets/seafarer/lang/en.json` — trait names, rarity names, line templates, two new chat messages.

## Open Questions

None at design time. Tuning of trait values may adjust during playtest.
