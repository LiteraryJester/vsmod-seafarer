# Harpoon Weapon — Design

**Date:** 2026-05-09
**Status:** Approved

## Summary

Add a thrown harpoon weapon to Seafarer. Functions as a javelin (charge-aim, throw, deal damage on hit) but on a successful hit has a material-scaled chance to *tether* the struck entity to the thrower with a physical rope. While tethered, the rope drags the entity back toward the thrower if it strays. The rope breaks if stretched past its tolerance, leaving the harpoon embedded in the entity in a "no-rope" state; the player can retrieve and re-rope it to throw again.

Crafted from a smithed harpoon head + stick + rope. Material-tiered: copper, tinbronze, bismuthbronze, blackbronze, iron, meteoriciron, steel — same set as the base-game spear.

## Motivation

Seafarer's gameplay leans into ocean and coastal hunting — clams, fish, eventually larger marine creatures. A harpoon fits the genre and gives players an alternative to bow/spear for hunting moving targets, with the tether mechanic creating the whaling fantasy: the prey doesn't just die or escape, it gets dragged.

The base game already has all the rope physics infrastructure (`ClothSystem`, `ClothManager`, `EntityBehaviorRopeTieable`) used by `ItemRope` to leash tame animals to fences. The harpoon reuses this — projectile, rope-tying, max-stretch-then-break — so most of the work is wiring existing systems together.

## Design

### 1. Items

Two distinct item codes per metal — VS grid recipes can't filter ingredients by ItemStack attributes, so the rope/no-rope distinction has to live in the item code, not in attributes. Using two items also means handbook entries, durability, and shape variants are all driven cleanly by the `code` variant pattern instead of needing attribute-conditional rendering.

#### `harpoon-{metal}`

The thrown weapon, rope-equipped. Variants by metal: `copper`, `tinbronze`, `bismuthbronze`, `blackbronze`, `iron`, `meteoriciron`, `steel`.

Per-material attributes (tunable in JSON):
- `damageByType` — thrown damage, mirrors base spear damage table for the same metal.
- `tetherChanceByType` — probability of tether on a successful entity hit.
  - copper 0.30
  - tinbronze 0.40
  - bismuthbronze 0.45
  - blackbronze 0.55
  - iron 0.60
  - meteoriciron 0.65
  - steel 0.70
- `harpoonEntityCodeByType` — `seafarer:harpoon-projectile-{metal}` (parallels `arrowEntityCodeByType` on `arrow-barbed`).
- `breakChanceOnImpactByType` — small chance the harpoon breaks on hitting a hard surface, like arrows.

Class: `"class": "ItemHarpoon"` — a new C# class extending `ItemSpear`.

#### `harpoon-norope-{metal}`

The same weapon after the rope has been broken or otherwise lost. Same metal variants, same class. Differences:
- No `tetherChanceByType` — bare harpoon never tethers, even if it lands a hit.
- Same `damageByType` as the roped version (still hits as hard).
- Visually identical to `harpoon-{metal}` but without the coiled rope on the haft (separate shape file).

Both `harpoon-{metal}` and `harpoon-norope-{metal}` use the same `ItemHarpoon` class; the class checks the item's own code path (or a JSON flag like `isRoped: true`) to decide whether to spawn a tether-capable projectile.

#### `harpoon-head-{metal}`

The smithable head. Variants by metal (same set as above). Pure resource item — no behavior, no class. Mirrors `arrowhead-barbed-{metal}` in shape and treatment.

### 2. Recipes

#### Smithing recipe (per metal)

`assets/seafarer/recipes/smithing/harpoonhead.json` — produces `harpoon-head-{metal}` from a hot ingot. Pattern based on `arrowhead-barbed.json` (mostly a forge-out-of-an-ingot voxel pattern). Same material substitution.

#### Crafting recipe (per metal)

3×1 vertical grid in `recipes/grid/harpoon.json`:

```
H
S
R
```
- `H` — `seafarer:harpoon-head-*` (`name: "metal"`)
- `S` — `game:stick`
- `R` — `game:rope`

Output: `seafarer:harpoon-{metal}` quantity 1.

#### Re-rope recipe (per metal)

2×1 vertical grid in `recipes/grid/harpoon-rerope.json`:

```
H
R
```
- `H` — `seafarer:harpoon-norope-*` (`name: "metal"`)
- `R` — `game:rope`

Output: `seafarer:harpoon-{metal}` quantity 1.

This recipe maps the rope-less variant + rope back to the rope-equipped variant. The `*`-wildcard with `name: "metal"` and matching `{metal}` in the output preserves the metal tier across the conversion (same pattern base-game armor-repair recipes use).

### 3. Throwing — `ItemHarpoon`

`ItemHarpoon : ItemSpear` (extends, doesn't reimplement). Inherits:
- Aim animation on `OnHeldInteractStart`.
- Cancel/release lifecycle on `OnHeldInteractStop`.
- Damage / charge math from base `ItemSpear`.

Overrides:
- The projectile-spawning step inside `OnHeldInteractStop`. Base `ItemSpear` reads `spearEntityCodeByType` and creates a generic projectile entity. We replace that with our own logic that:
  - Reads `harpoonEntityCodeByType` for the entity code.
  - Spawns an `EntityProjectileHarpoon`.
  - Sets a `HasRope` flag on the entity, derived from the item type (`true` for `harpoon-*`, `false` for `harpoon-norope-*`). The check looks at the item's `Code` for the `-norope-` segment, OR reads an `isRoped` JSON attribute on the item type — whichever the implementer prefers, both are deterministic from the item code.
  - Records the thrower's entity ID.

Both `harpoon-{metal}` and `harpoon-norope-{metal}` use this same class. The bare harpoon throws with full damage but skips all tether logic on impact. `OnHeldInteractStart` does NOT block the throw based on rope state.

### 4. Projectile — `EntityProjectileHarpoon`

`EntityProjectileHarpoon : EntityProjectile`. Stored fields (synced via `WatchedAttributes`):
```csharp
public bool HasRope;            // set at spawn from the throwing item's code (true for harpoon-*, false for harpoon-norope-*)
public long ThrowerEntityId;    // copied from FiredBy at spawn
```

Override `ImpactOnEntity(Entity entity)`:
1. Call `base.ImpactOnEntity(entity)` — applies thrown damage as the base does.
2. Server-side only: if `World.Side != EnumAppSide.Server` return.
3. If `entity == null || !entity.Alive` return (no tether on a corpse).
4. If `!HasRope` return (bare harpoon already used its rope).
5. Read `tetherChanceByType` for our material from item attributes (already serialized into the entity's properties).
6. Roll: if `World.Rand.NextDouble() > chance` return.
7. Tether-proc:
   - Look up the thrower entity by `ThrowerEntityId`. If null/dead, abort.
   - Get the target's `EntityBehaviorRopeTieable`. If absent, attach one at runtime: `entity.AddBehavior(new EntityBehaviorRopeTieable(entity))`.
   - Get the `ClothManager` mod system.
   - Call `ClothSystem.CreateRope(api, cm, throwerEyePos, targetCenterPos, null)` to create the rope.
   - Pin `FirstPoint` to the thrower (`PinTo(throwerEntity, offset)`); pin `LastPoint` to the target's `EntityBehaviorRopeTieable` (use the same protocol `ItemRope.attachToEntity` does).
   - Register the cloth: `cm.RegisterCloth(rope)`.
   - Notify the new `RopeTetherTracker` mod system: `tracker.RegisterTether(new TetherRecord { ClothId, HarpoonEntityId, ThrowerEntityId, TargetEntityId, NominalLength = 16, BreakLength = 24 })`.

Override `ImpactOnBlock` — no tether logic, just standard stuck-projectile behavior. Ground-stuck harpoon with a rope still has its `HasRope` attribute set; if a player picks it up, they get a `hasRope: true` ItemStack.

### 5. Rope-stretch tracker — `RopeTetherTracker` ModSystem

A new server-side mod system. Maintains an in-memory list of active tethers. Ticks every 1.0 seconds (slow tick — the rope physics handles instant stretch; we only need to detect "too far" to break it).

```csharp
public class TetherRecord
{
    public int ClothId;
    public long HarpoonEntityId;
    public long ThrowerEntityId;
    public long TargetEntityId;
    public float NominalLength;
    public float BreakLength;
}
```

Per-tick logic:
1. For each `TetherRecord`:
   - Look up the rope via `cm.GetClothSystem(ClothId)`. If null (already torn / unregistered), drop the record.
   - Look up the two pinned entities. If either is null or dead, break (see below) and drop.
   - Compute straight-line distance between the two pinned points (rope endpoints in world space).
   - If `distance > BreakLength`, break the rope.
2. Break logic:
   - Unpin both ends, unregister cloth via `cm.UnregisterCloth(ClothId)`.
   - Find the harpoon entity by `HarpoonEntityId`. Update its drop ItemStack so on retrieval the player gets `seafarer:harpoon-norope-{metal}` instead of `seafarer:harpoon-{metal}`. Implementation: stick the embedded harpoon's projectile entity has a `Drops` ItemStack array (or equivalent on `EntityProjectile`); swap that reference. If the projectile entity has already been removed (e.g. target took multiple hits and the harpoon despawned), no-op.
   - Drop the record from the tracker.

Persistence: tethers are not persisted across save/load — on world shutdown, in-flight tethers break gracefully (the cloth system itself isn't saved either). On load, anyone with a stuck-in-target harpoon still has it physically; the rope is gone. Acceptable simplification.

### 6. Material variants

Same set as base-game spear's metal tier: `copper`, `tinbronze`, `bismuthbronze`, `blackbronze`, `iron`, `meteoriciron`, `steel`. No stone tier (the stone-tier spear materials don't fit a harpoon's use case — sea-creature hunting is a metal-age activity in this mod's progression).

`variantgroups` on the JSON drives this; per-material balance lives in `damageByType` / `tetherChanceByType` / `breakChanceOnImpactByType` / `durabilityByType`.

### 7. Visuals

Two shapes per metal under `shapes/item/tool/harpoon/`:
- `harpoon-rope-{metal}.json` — haft + head + coiled rope around the haft.
- `harpoon-norope-{metal}.json` — haft + head only.

Switched at render time via `shapeIfAttribute` (or `shapeByType` + a per-attribute resolver). Same texture set; the rope coil is a separate cube/element in the rope variant.

Initial implementation: reuse the base-game spear shape for the haft+head silhouette; add a simple cylinder/cube for the rope coil. Custom art can come later — out of scope for the first cut.

### 8. Animation

Reuse base-game spear animations (`spearidle`, `aim`, `spearhit`) — already wired by `ItemSpear`. No new animation work.

## Edge cases

- **Thrower goes offline mid-tether.** The thrower entity becomes inactive but still exists in the saved state. The tracker's tick will see `thrower.Alive == false` (or `thrower == null` if despawned) on next tick → break the rope. Acceptable: rope just snaps as if the thrower had moved out of range.
- **Target enters an unloaded chunk.** Entity becomes inactive. Tracker tick sees the entity is null → break. Same outcome.
- **Multiple harpoons in same target.** Each tether is a separate `TetherRecord`. All exist independently. Breaking one doesn't affect others.
- **Tether to entities that already have `EntityBehaviorRopeTieable`.** No double-add; check before attaching.
- **Tether attempts to non-tetherable entity types** (e.g. items, projectiles, bosses configured immune). The server-side runtime add of `EntityBehaviorRopeTieable` is unconditional — but we should respect a flag. Add an early-out: if entity has `Properties.Attributes["harpoonImmune"].AsBool(false) == true`, skip tether. Default false; can be set to true on configured entity types later.
- **Hitting an entity that's already tethered by this same thrower.** The new throw's tether-proc creates a second rope. Both exist. Acceptable; player's burden to manage.
- **Bare harpoon retrieve from ground.** `EntityProjectile` already drops the source ItemStack on retrieval. The drop is `harpoon-{metal}` for a roped throw or `harpoon-norope-{metal}` for a bare throw, set at projectile spawn time and updated on rope-break (see Section 5). Retrieval just gives the player whichever code the projectile entity is currently carrying.

## Out of scope (deferred)

- Stone-tier harpoon variants.
- Custom 3D animations / new spear-distinct throw animation.
- Pulling logic that yanks the *thrower* toward the target (instead of vice versa) — current design always drags target toward thrower, matching base-game leash behavior.
- Multi-player tether handoff (passing the rope to another player).
- Boat-mounted harpoon stations (mounting a harpoon on a logbarge so the rope tethers to the boat instead of the player).
- Visual rope rendering in first-person view if the cloth system doesn't already do it. Trust base-game render for now.

## Files changed / created

**New C# code:**
- `Seafarer/Seafarer/Item/ItemHarpoon.cs`
- `Seafarer/Seafarer/Entity/EntityProjectileHarpoon.cs`
- `Seafarer/Seafarer/Systems/RopeTetherTracker.cs`

**Modified C# code:**
- `Seafarer/Seafarer/SeafarerModSystem.cs` — register `ItemHarpoon`, `EntityProjectileHarpoon`, `RopeTetherTracker`.

**New JSON assets:**
- `assets/seafarer/itemtypes/tool/harpoon.json`
- `assets/seafarer/itemtypes/toolhead/harpoonhead.json`
- `assets/seafarer/recipes/grid/harpoon.json`
- `assets/seafarer/recipes/grid/harpoon-rerope.json`
- `assets/seafarer/recipes/smithing/harpoonhead.json`
- `assets/seafarer/entities/nonliving/projectile/harpoon.json`
- `assets/seafarer/shapes/item/tool/harpoon/harpoon-rope.json` (per-metal-textured at render)
- `assets/seafarer/shapes/item/tool/harpoon/harpoon-norope.json`
- `assets/seafarer/shapes/item/toolhead/harpoonhead.json`
- Texture entries under `assets/seafarer/textures/item/tool/harpoon/` and `.../toolhead/harpoonhead/`.

**Modified asset files:**
- `assets/seafarer/lang/en.json` — display strings (`item-harpoon-{metal}`, `item-harpoonhead-{metal}`, plus tooltip strings for tether chance, rope-state, etc.).
- `assets/seafarer/config/handbook/` — optional handbook page describing the harpoon's tether mechanic. Out of scope for first cut; add later.

## Testing

No automated tests exist for in-game weapon behavior in this repo. Verification:
1. Build cleanly.
2. Asset validator passes.
3. In-game manual test:
   - Craft a `harpoon-head-copper` → smithing recipe works.
   - Craft a harpoon (head+stick+rope) → grid recipe produces the rope variant.
   - Throw it at a sheep / wolf / drifter — projectile flies with javelin physics; hit registers damage.
   - Confirm tether triggers on some hits and not others (depends on per-material chance — at copper 30%, expect ~3-in-10).
   - On a tethered hit, see the rope visually connecting player to entity. Walk around — entity should drag.
   - Run away to break rope (>24 blocks). Confirm rope visually snaps; harpoon stays in target; retrieving gives a no-rope harpoon.
   - Re-rope via grid recipe. Confirm `hasRope: true` returned.
   - Test on a higher-tier harpoon (steel) for the increased tether rate.
4. Server log: no errors, no NREs, no "tried to set block outside generating chunks" or similar.
