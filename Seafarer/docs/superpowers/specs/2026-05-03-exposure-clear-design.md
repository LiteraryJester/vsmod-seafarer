# Exposure Clear on Disable / Death

## Problem

`EntityBehaviorExposure` accumulates a per-player `level`, `condition`, and
`tier`, applies stat modifiers (`exposurePenalty` on walkspeed, healing,
hunger, max health, thirst), and adjusts temporal stability at heatstroke III.
Two situations leave that state stuck on the player:

1. **System disabled.** Toggling `EXPOSURE_ENABLED` off (via ConfigLib)
   makes `OnGameTick` early-return, but it does not clear the existing
   `level`, `tier`, or stat modifiers. A player who had Frostbite II at
   the moment of disable keeps the −6 max-health penalty indefinitely.

2. **Player death.** Death does not currently touch exposure. Respawned
   players resume with whatever level / tier / penalty they had at death.

## Goal

Clear exposure state and all derived effects when:

- A player entity is loaded from disk (reconnect after a disconnect) **and**
  `Config.Enabled` is false.
- A player dies, **regardless** of `Config.Enabled`.

Together these two triggers cover every reachable state: a cold-loaded player
sees the disable-clear; a player who dies while the system is enabled sees the
death-clear; a player who dies while the system is disabled is covered by both
(death-clear runs first and is sufficient).

## Non-goals

- Reacting live to a mid-session ConfigLib toggle. The clear is observed on
  the next entity load, which covers the typical case (toggle off → leave →
  rejoin) and the disable-after-death case.
- Sub-toggle granularity. The master `Enabled` flag drives the clear; the
  `HeatstrokeEnabled` / `FrostbiteEnabled` sub-toggles are unchanged.
- Client-side state clearing. Exposure state lives in server-authoritative
  `WatchedAttributes`; the existing sync propagates the cleared values.

## Design

All changes are in `EntityBehavior/EntityBehaviorExposure.cs`. No new types,
no new persisted fields, no new mod-system wiring.

### Trigger 1 — load with system disabled

Override `OnEntityLoaded()`. Server-side only. If `!Config.Enabled`, call
`ClearAllEffects()`.

`OnEntityLoaded` fires after `Initialize` when a saved entity is loaded from
disk — for players this means reconnect after a disconnect. It does NOT fire
on first spawn into a new world (that path is `OnEntitySpawn`), and it does
NOT fire on in-session death/respawn (that path is `OnEntityRevive`).
Initialize is not the right hook because `expTree` is being created there for
fresh entities; the load hook runs once `WatchedAttributes` is fully populated
from save.

The fresh-spawn gap is harmless: a fresh entity has level 0 and no stat
modifiers, so there is nothing to clear. The in-session-death gap is covered
by Trigger 2 below.

### Trigger 2 — death

Override `OnEntityDeath(DamageSource damageSourceForDeath)`. Server-side
only. Call `ClearAllEffects()` unconditionally. Death wipes exposure even
when the system is enabled — the rationale is gameplay symmetry with
hunger / health / temperature, all of which the base game resets on death.

### `ClearAllEffects()` adjustment

Currently:

```csharp
private void ClearAllEffects()
{
    if (ExposureLevel > 0 || ActiveTier > 0)
    {
        RemoveTierEffects(ActiveTier);
        ExposureLevel = 0f;
        ActiveCondition = ExposureCondition.None;
        ActiveTier = 0;
    }
}
```

Two adjustments:

1. **Also reset `LastUpdateTotalHours = api.World.Calendar.TotalHours`** so
   that re-enabling later does not feed a large `hoursPassed` into the next
   `UpdateExposure`. The 24-hour cap guard already softens this, but resetting
   at clear time is cleaner and removes a class of "exposure jumps on
   re-enable" surprise.
2. **Also reset `lastAppliedCondition = ExposureCondition.None`** so the next
   non-zero tier transition is treated as a fresh condition switch. Without
   this, `UpdateTierEffects` could short-circuit a re-application after a
   clear-then-re-accumulate cycle within the same session.

The early-return `if (ExposureLevel > 0 || ActiveTier > 0)` should be
broadened to include the timestamp/lastApplied resets, or removed — calling
the clear when state is already zero is harmless. Removing it is simpler.

### Reuse of existing helpers

- `RemoveTierEffects(ActiveTier)` already restores the temporal-stability
  offset for the heatstroke-III case.
- `RemoveAllStatModifiers()` is called from `RemoveTierEffects`, so all five
  stat keys are cleared.
- `OnEntityDespawn` already calls `RemoveAllStatModifiers()` independently;
  it is unchanged.

## Files touched

- `Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs`
  - Add override `OnEntityLoaded()`.
  - Add override `OnEntityDeath(DamageSource)`.
  - Adjust `ClearAllEffects()` to reset `LastUpdateTotalHours` and
    `lastAppliedCondition`, drop the early-return guard.

## Validation

Manual test plan additions (no automated tests for behaviors in this repo):

1. Heatstroke II accumulated → toggle `EXPOSURE_ENABLED` off in ConfigLib →
   relog → confirm max-health penalty / walkspeed penalty are gone, env-text
   exposure line is gone.
2. Frostbite II accumulated → die to a wolf → respawn → confirm exposure
   level is 0, no max-health penalty, no env-text exposure line.
3. Heatstroke III accumulated → die → confirm temporal-stability returns to
   normal (no lingering −0.5 offset).
4. Sanity: with `EXPOSURE_ENABLED` on, normal accumulation still works after
   a death-clear (no stuck `lastAppliedCondition` blocking re-application).

Build with `dotnet build Seafarer/Seafarer.csproj` (project requires
`VINTAGE_STORY` env var per `CLAUDE.md`).
