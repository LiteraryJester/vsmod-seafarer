# Boat & Raft Health Design

**Date:** 2026-04-28
**Status:** Approved

## Goal

Give boats and rafts hit points so they can take damage from combat, high-speed collisions, and storms, with a JSON-driven config that applies to all four target vessels: seafarer's `boat-outrigger` and `boat-logbarge`, and vanilla's `boat-raft` and `boat-sailed`. Repair is performed in-world by right-clicking the boat with a liquid container of glue or marine varnish.

## Scope

In scope:

- Per-boat HP, configured via a single attributes block: `attributes.extendShipMechanics`.
- Combat damage (passive — vanilla `health` behavior handles it).
- Collision damage from running into terrain at speed.
- Storm damage while in deep water during high winds.
- Wreckage drop on death — partial deconstruct materials spawn at the death position.
- Field repair via glue or marine varnish, configured on the items themselves.
- JSON patches to apply the system to vanilla `boat-raft` and `boat-sailed`.

Out of scope:

- Beached / dry-rot damage (deferred — easy to add later via a new config block on `EntityBehaviorShipMechanics`).
- Visual damage states (cracked planks at low HP). Not requested.
- Repair stations / dry docks. Field repair only.
- Automated tests — the mod has no test framework; verification is manual playtest.

## Architecture

Two new code units; everything else is JSON.

### `EntityBehaviorShipMechanics`

Server + client behavior, registered as `shipmechanics`. Lives alongside the standard `health` behavior.

Responsibilities:

- On `Initialize`: read `attributes.extendShipMechanics`, locate the sibling `EntityBehaviorHealth`, overwrite its `currenthealth` and `maxhealth` from the config. HP then lives in the standard place and networks the standard way.
- Server-only game-tick handler (1 Hz): collision-damage detection and storm-damage application.
- Hook `entity.OnEntityDeath` to spawn wreckage drops.

Why a separate behavior alongside `health` instead of replacing it: the vanilla `EntityBehaviorHealth` carries network sync, damage-source filtering, and OnDeath plumbing that other mods inspect. Reimplementing it risks subtle multiplayer bugs and breaks mod compatibility.

### `CollectibleBehaviorBoatRepair`

Item-side `CollectibleBehavior`, registered as `boatrepair`. Patched onto repair items (glue, marine varnish, future materials).

Responsibilities:

- Hook `OnHeldInteractStart` / `OnHeldInteractStep` / `OnHeldInteractStop`.
- When the player right-click-and-holds the item on an entity that has `EntityBehaviorShipMechanics`:
  1. Verify the held item is a liquid container (`BlockLiquidContainerBase.GetContent`) holding a non-empty stack of the matching liquid.
  2. Set `handHandling = PreventDefault`, start a 1.5-second use animation, play one of the vanilla `gluerepair1-4.ogg` sounds.
  3. On full hold: drain `litresPerUse` from the container via the standard `WaterTightContainerProps` API, increment the boat's HP by `hpPerLitre × litresPerUse`, clamped to max.
  4. Block use entirely (with a fail message) if the boat is already at full HP.

The behavior is item-agnostic — it acts on whatever item it's patched onto. Per-item tuning lives in the patch (`hpPerLitre`, `litresPerUse`), so glue and varnish feel different without C# changes.

## JSON Config Schema

Single attributes block on the boat entity:

```json
"attributes": {
  "extendShipMechanics": {
    "health": 200,

    "collision": {
      "minSpeed": 0.30,
      "damagePerSpeedUnit": 8.0,
      "cooldownSeconds": 1.0
    },

    "storm": {
      "minWindSpeed": 0.65,
      "damagePerSecond": 0.4,
      "requiresDeepWater": true
    },

    "wreckage": {
      "dropFraction": 0.4,
      "dropFloating": true
    }
  }
}
```

### Field semantics

| Field | Meaning |
|---|---|
| `health` | Used as both `currenthealth` and `maxhealth` at spawn. |
| `collision.minSpeed` | Velocity-magnitude (blocks/sec) below which a collision causes no damage. |
| `collision.damagePerSpeedUnit` | `damage = (speed − minSpeed) × damagePerSpeedUnit` per impact. |
| `collision.cooldownSeconds` | Debounce so one impact = one damage event. |
| `storm.minWindSpeed` | Vanilla wind speed threshold (0–1) above which storm damage ticks. |
| `storm.damagePerSecond` | Applied per server tick, scaled by tick interval. |
| `storm.requiresDeepWater` | When true, only ticks when water depth at boat ≥ 3 blocks. |
| `wreckage.dropFraction` | Fraction of `deconstructDrops` quantities to spawn on death. |
| `wreckage.dropFloating` | When true, drops get +0.1 Y velocity to bob on the surface. |

### Defaults

Any omitted field falls back to the engine default (matches the values shown above, except `health` which has no default — must be specified).

### Per-boat values

| Boat | health | wreckage.dropFraction |
|---|---|---|
| vanilla `boat-raft` | 60 | 0.5 |
| vanilla `boat-sailed` | 120 | 0.4 |
| seafarer `boat-logbarge` | 100 | 0.5 |
| seafarer `boat-outrigger-seasoned` | 200 | 0.4 |
| seafarer `boat-outrigger-varnished` | 280 | 0.4 |

Collision and storm tunables are identical across all four — same physics rules.

### Repair-item config

Patched onto each repair item's `behaviors` array:

```json
{ "code": "boatrepair", "hpPerLitre": 20.0, "litresPerUse": 0.25 }
```

Per-item proposed values:

| Item | hpPerLitre | litresPerUse | HP per click |
|---|---|---|---|
| `glueportion` (vanilla) | 20 | 0.25 | 5 |
| `varnishportion-marine` (seafarer) | 50 | 0.25 | 12.5 |

## Damage Sources

### Combat damage

Fully passive. Once `EntityBehaviorHealth` is on the entity, vanilla weapon and projectile hits route through `Entity.ReceiveDamage` and decrement HP. The seafarer mod's existing `EntityProjectileBarbed.OnCollideEntity` already calls `ReceiveDamage`; it begins to matter once boats have HP.

### Collision damage

In the server-tick handler:

1. Sample the entity's prior-frame horizontal speed (cached from the previous tick).
2. On the tick where `entity.CollidedHorizontally` becomes true and prior-frame speed exceeded `minSpeed`, apply damage: `(priorSpeed − minSpeed) × damagePerSpeedUnit`.
3. Use `DamageSource { Source = SourceBlock, Type = Crushing }`.
4. Start a cooldown timer; ignore further collision events until `cooldownSeconds` has elapsed.

This ignores slow nudging into the shore (below `minSpeed`) and prevents one crash from registering as 20.

### Storm damage

Each server tick:

1. Get wind speed at the boat's position via `WeatherSystem.GetWindSpeed(blockPos)`.
2. If wind speed ≥ `minWindSpeed`:
   - If `requiresDeepWater` is true, sample the block 3 below the boat's swimming offset; require it to be a liquid block.
   - If checks pass: apply `damagePerSecond × tickIntervalSeconds` damage with `DamageSource { Source = SourceVoid, Type = Other }`. `SourceVoid` keeps the damage from being attributed to a player or mob.

## Death and Wreckage

`EntityBehaviorShipMechanics.Initialize` subscribes to `entity.OnEntityDeath`. Server-side handler:

1. Read `entity.Properties.Attributes["deconstructDrops"]`, falling back to `deconstructDropsByType` keyed by the entity's variant code (logbarge uses the by-type form).
2. Resolve each drop entry against variant codes so `{material}` placeholders expand correctly.
3. For each resolved stack, multiply `quantity` by `wreckage.dropFraction`. Take `Math.Floor`. For entries that round to 0, promote to 1 with probability `quantity × dropFraction` (so a "1 rope" entry with 0.5 fraction gives a 50% chance of 1 rope, not always 0).
4. Spawn each stack via `world.SpawnItemEntity` at the death position. If `dropFloating`, set initial velocity Y to +0.1.

Mounted players, seats, and rideable-accessory contents are not our concern — vanilla `EntityBoat`'s death handling already drops accessory contents (`dropContentsOnDeath: true` is set in the JSONs) and dismounts riders.

## Vanilla Boat Patches

Two files in `Seafarer/Seafarer/assets/seafarer/patches/`:

`vanilla-boat-raft-shipmechanics.json` — adds `health` and `shipmechanics` to both client and server behaviors arrays, and sets `attributes.extendShipMechanics` with `health: 60, dropFraction: 0.5`.

`vanilla-boat-sailed-shipmechanics.json` — same shape, `health: 120, dropFraction: 0.4`.

Each patch is JSON-Patch format, targets the vanilla file with `"file": "game:entities/nonliving/boat-raft"` (or `boat-sailed`), and uses `op: add` against `/server/behaviors/-`, `/client/behaviors/-`, and `/attributes/extendShipMechanics`.

## Item Patches

Two files in `Seafarer/Seafarer/assets/seafarer/patches/`:

`glueportion-boatrepair.json` — patches vanilla `glueportion` to add `{ "code": "boatrepair", "hpPerLitre": 20.0, "litresPerUse": 0.25 }`. Creates the `behaviors` array if absent.

`varnishportion-marine-boatrepair.json` — patches the seafarer item to add `{ "code": "boatrepair", "hpPerLitre": 50.0, "litresPerUse": 0.25 }`.

(The seafarer item's patch could alternatively be inlined into its definition; using a patch keeps both repair items registered the same way and easy to find.)

## Behavior Registration

`SeafarerModSystem.Start`:

```csharp
api.RegisterEntityBehaviorClass("shipmechanics", typeof(EntityBehaviorShipMechanics));
api.RegisterCollectibleBehaviorClass("boatrepair", typeof(CollectibleBehaviorBoatRepair));
```

## Boat Entity JSON Edits (seafarer)

Direct edits to two files (no patches needed since they're our own assets):

- `boat-outrigger.json`: add `{ "code": "health" }` and `{ "code": "shipmechanics" }` to both `client.behaviors` and `server.behaviors`. Add the `extendShipMechanics` block to `attributes`. Use `healthByType` to vary HP between seasoned and varnished variants.
- `boat-logbarge.json`: same shape; single material variant so `health: 100` flat.

## Tunable Constants in Code

- `EntityBehaviorShipMechanics.TickIntervalMs = 1000` — server tick rate.
- Wreckage fractional-promotion math is the only non-trivial logic beyond "multiply, floor, spawn." Everything else reads cleanly from config.

## Testing Strategy

No automated tests — matches the rest of the codebase. Manual playtest checklist:

1. Spawn each of the 4 boats; verify HP shows in F3 debug and matches per-boat config.
2. Hit each boat with a sword and arrows; verify HP decrements (combat works passively).
3. Ram each boat into a cliff at full speed; verify collision damage triggers. Verify slow nudging into terrain does NOT trigger.
4. Place a boat in deep water during a storm event (`/weather setprecip storm`); verify HP slowly ticks down. Verify the same boat on land or in shallow water does NOT tick.
5. Hold a glue bucket on a damaged boat; verify HP restored, glue litres consumed, repair sound plays. Same for marine varnish; verify varnish heals more per use.
6. Try to repair a full-HP boat; verify it's blocked with a fail message.
7. Kill each boat from full HP via repeated arrows; verify wreckage spawns at the death position with the configured fraction of materials, items bob in water if `dropFloating` is true.
8. Mount a boat, kill it; verify the rider is dismounted (vanilla behavior).

`validate-mod-assets` runs as part of the change set to catch JSON-side errors (lang entries, recipe consistency, etc.) before build.

## Files

New code:

- `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs`
- `Seafarer/Seafarer/CollectibleBehavior/CollectibleBehaviorBoatRepair.cs` (new directory)

New JSON:

- `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-raft-shipmechanics.json`
- `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-sailed-shipmechanics.json`
- `Seafarer/Seafarer/assets/seafarer/patches/glueportion-boatrepair.json`
- `Seafarer/Seafarer/assets/seafarer/patches/varnishportion-marine-boatrepair.json`

Modified:

- `Seafarer/Seafarer/SeafarerModSystem.cs` — register the two new behavior classes.
- `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-outrigger.json` — add behaviors and attributes.
- `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-logbarge.json` — add behaviors and attributes.
- `Seafarer/Seafarer/assets/seafarer/lang/en.json` — error/HUD strings for repair (full-HP message, etc.).

## Open Questions

None at design time. Tuning of HP values and damage rates will adjust during playtest.
