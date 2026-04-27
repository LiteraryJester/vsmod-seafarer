# Exposure System Simplification — Design

**Status:** Approved 2026-04-26
**Target file:** `Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs`

## Problem

The current exposure system causes lag spikes. Per-player work in `OnGameTick`:

- `RoomRegistry.GetRoomForPosition(plrpos)` every 3s — the dominant cost; flood-fills room volumes.
- `BlockAccessor.GetClimateAt(...)` every 1s — ~60 climate samples/min/player.
- `BlockAccessor.GetWindSpeedAt(...)` every 1s.
- `BlockAccessor.GetRainMapHeightAt(...)` every 3s.

Three independent accumulators (`accum`, `slowAccum`, `damageAccum`) drive these.

## Goal

Reduce per-player tick work to a single climate sample every 5 real-minutes plus the existing 10s damage tick at Tier 3. Drop the room and skylight checks entirely — exposure is now purely a function of the temperature sample at the player's position.

## Design

### Tick cadence

- One **`accumulator`** field replaces `accum` + `slowAccum`. Fires every **300s real-time**.
- **`damageAccumulator`** retains its 10s cadence for Tier 3 damage and is unchanged.
- Both run server-only inside `OnGameTick`. Early-return when `!Config.Enabled` is preserved.

### State removed

- Fields: `inEnclosedRoom`, `exposedToSky`.
- Method: `UpdateShelterState()`.
- All `isSheltered` branching in `UpdateExposure()`.
- The `plrpos` field stays but is only updated inside the 5-min branch (no longer every frame).

### `UpdateExposure()` simplified flow

1. Resolve `EntityPlayer` / `IPlayer`. Skip if creative or spectator (call `ClearAllEffects()`).
2. Set `plrpos` from the entity's current position at half eye height.
3. Read climate (`EnumGetClimateMode.NowValues`) and wind once. If climate is null, return.
4. Compute `hoursPassed = TotalHours - LastUpdateTotalHours`:
   - If `< 0`: clock went backwards (world load / restore). Reset `LastUpdateTotalHours` and return without accumulating.
   - If `< 0.001`: return.
   - Clamp upper bound to **24 game-hours** (was 1) so the first tick after a reload represents at most one game day rather than the full elapsed time.
   - Then write back `LastUpdateTotalHours = currentHours`.
5. Branch on temperature:
   - `Config.HeatstrokeEnabled && temp >= Config.HeatThreshold` → `severity = (temp - HeatThreshold) / 10f`; `change = AccumulationRatePerHour * (1 + severity) * windMultiplier * hoursPassed`; `ActiveCondition = Heatstroke`.
   - Else `Config.FrostbiteEnabled && temp <= Config.ColdThreshold` → mirror image with `(ColdThreshold - temp) / 10f`; `ActiveCondition = Frostbite`.
   - Else → `change = -DecayRatePerHour * hoursPassed` (decay toward 0 like today).
6. Clamp `ExposureLevel + change` to `[0, 1]`, write back.
7. If new level `<= 0`, set `ActiveCondition = None`.
8. Call `UpdateTierEffects()` (unchanged).

### Damage tick — unchanged

`ApplyPeriodicEffects()` keeps its 10s cadence. At Tier 3 the player still takes a damage hit every 10s; only the *accumulation* check is lazy.

### Healing interception — unchanged

`OnEntityReceiveDamage` for `EnumDamageType.Heal` still reduces exposure proportionally and refreshes tier effects immediately, so healing items don't have to wait 5 min to take effect.

### Wind multiplier and severity — unchanged

`Config.WindAccumulationMultiplier` and the `(temp - threshold) / 10f` severity scaling are preserved. Behavior at the same instant in time is identical to today, just sampled less often.

### Config — unchanged

No new fields. `AccumulationRatePerHour` and `DecayRatePerHour` are still per-game-hour rates and continue to scale linearly with `hoursPassed`.

## Behavior changes for players

- **Shelter no longer protects.** A player in a desert house gets heatstroke if the climate engine reports the same temperature inside as outside. Caves in hot biomes also offer no protection.
- **Stat-penalty latency.** Walkspeed / max-HP / hunger penalties only refresh on the 5-min boundary. Walking out of a hot zone keeps the penalty until the next sample.
- **Damage latency.** `ApplyPeriodicEffects` reads `ActiveTier`, which only updates on the 5-min boundary. A player who drops below Tier 3 (e.g., by walking into shade) keeps taking Tier 3 damage every 10s until the next sample clears the tier. The healing path bypasses this — using a healing item refreshes tier effects immediately.
- **First check after world load** can apply up to 24 game-hours of accumulation in one shot if temperature is out of range. This is a deliberate cap (see step 4 in the flow) so a long offline period doesn't translate to instant max exposure.
- **Healing still feels responsive** because the heal interception path is unchanged.

## Performance impact (per player)

| Call | Before | After |
|---|---|---|
| `RoomRegistry.GetRoomForPosition` | 20/min | 0 |
| `GetClimateAt` | 60/min | ~0.2/min |
| `GetWindSpeedAt` | 60/min | ~0.2/min |
| `GetRainMapHeightAt` | 20/min | 0 |
| `ReceiveDamage` (Tier 3) | 6/min | 6/min |

## Out of scope

- Persistence bug ("Exposure resets to 0 on world load") tracked in `Seafarer/CLAUDE.md` — separate fix.
- Any change to `ExposureCondition` enum, `ExposureConfig` fields, or tier-effect formulas.
- Any UI / HUD changes.
