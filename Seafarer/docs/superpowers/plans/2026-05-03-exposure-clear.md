# Exposure Clear on Disable / Death — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `EntityBehaviorExposure` clear all exposure state and stat
penalties when (a) the player entity loads and `Config.Enabled` is false,
and (b) the player dies (unconditional).

**Architecture:** Three changes inside
`Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs`. Reuse the
existing private `ClearAllEffects()` helper; add two new behavior overrides
(`OnEntityLoaded`, `OnEntityDeath`); broaden `ClearAllEffects()` so it also
resets the climate-tick timestamp and the cached `lastAppliedCondition`. No
new files, no new types, no asset or config changes.

**Tech Stack:** C# 12, .NET 10.0, Vintagestory.API entity-behavior pattern.

**Reference spec:** `Seafarer/docs/superpowers/specs/2026-05-03-exposure-clear-design.md`

**Test environment:** This repo has no automated test harness for entity
behaviors. Validation is `dotnet build` plus the in-game manual test plan in
the spec. Do not introduce a new test framework as part of this change.

**Build prerequisite (per `Seafarer/CLAUDE.md`):**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
```

`dotnet build` from `Seafarer/` resolves the game DLLs from this env var.

---

## File Structure

**Modified:** `Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs`

That is the only file touched by this plan.

Existing structure (relevant lines, before this plan):

- `Initialize(...)` — line 70 — sets up `expTree`, `LastUpdateTotalHours`, `hodInstalled`.
- `OnGameTick(...)` — line 87 — early-returns when `!Config.Enabled`.
- `UpdateExposure()` — line 109 — climate read + accumulation.
- `lastAppliedCondition` field — line 191 — caches the condition the current tier effects were applied for.
- `ClearAllEffects()` — line 351 — resets level/condition/tier when present.
- `OnEntityDespawn(...)` — line 362 — strips stat modifiers on despawn.

After this plan:

- `ClearAllEffects()` will additionally reset `LastUpdateTotalHours` and `lastAppliedCondition`, and run unconditionally.
- New `OnEntityLoaded()` override appended to the file.
- New `OnEntityDeath(DamageSource)` override appended to the file.

---

## Task 1: Broaden `ClearAllEffects()`

**Why first:** The two new overrides call `ClearAllEffects()`. Updating the
helper first means each subsequent task is a small, isolated addition.

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs:351-360`

- [ ] **Step 1: Replace the `ClearAllEffects()` body**

Find the current method (around line 351):

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

Replace with:

```csharp
    private void ClearAllEffects()
    {
        RemoveTierEffects(ActiveTier);
        ExposureLevel = 0f;
        ActiveCondition = ExposureCondition.None;
        ActiveTier = 0;
        lastAppliedCondition = ExposureCondition.None;
        LastUpdateTotalHours = api.World.Calendar.TotalHours;
    }
```

Notes:
- The early-return guard is removed. Calling `RemoveTierEffects(0)` is safe — it
  funnels into `RemoveAllStatModifiers()` (which calls `entity.Stats.Remove`
  on five keys; absent keys are no-ops) and skips the temporal-stability
  branch because `tier >= 3` is false.
- `lastAppliedCondition` is the private field declared at line 191. It is
  reachable from `ClearAllEffects()` since they are in the same class.
- `api` is the field set in `Initialize`. By the time `ClearAllEffects()` runs
  (only from `OnGameTick` callees, plus the new load/death hooks), `Initialize`
  has run and `api` is non-null.

- [ ] **Step 2: Build to verify the helper still compiles**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors. Warnings unchanged from baseline.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs
git commit -m "exposure: broaden ClearAllEffects to reset timestamp + cached condition

Drops the early-return guard and resets LastUpdateTotalHours and
lastAppliedCondition. Prepares the helper for use from new load and
death hooks."
```

---

## Task 2: Add `OnEntityLoaded` override (clear if disabled at load)

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs` — append a new override above the existing `OnEntityDespawn` override (around line 362).

- [ ] **Step 1: Insert the new override**

Locate the `OnEntityDespawn` override:

```csharp
    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        RemoveAllStatModifiers();
    }
```

Immediately *before* it, add:

```csharp
    public override void OnEntityLoaded()
    {
        if (api.Side != EnumAppSide.Server) return;
        if (!Config.Enabled) ClearAllEffects();
    }
```

Notes:
- `OnEntityLoaded()` is declared on the base class as
  `public virtual void OnEntityLoaded() { }`
  (verified in `vsapi/Common/Entity/EntityBehavior.cs` line 63).
- Server-side gate matches the existing pattern in `OnGameTick` and
  `UpdateExposure` — exposure state is server-authoritative.
- `EnumAppSide` and `Config` are already in scope (existing usings + the
  `SeafarerModSystem.ExposureConfig` static accessor exposed via the
  `Config` property at line 22).

- [ ] **Step 2: Build**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs
git commit -m "exposure: clear state on entity load when system is disabled

If Config.Enabled is false at the moment a player entity loads (join /
reconnect), wipe the persisted exposure level, condition, tier, and any
stuck stat modifiers."
```

---

## Task 3: Add `OnEntityDeath` override (always clear)

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs` — append a new override above `OnEntityDespawn`, after the `OnEntityLoaded` override added in Task 2.

- [ ] **Step 1: Insert the new override**

Place this directly after the `OnEntityLoaded` override added in Task 2 and before the existing `OnEntityDespawn` override:

```csharp
    public override void OnEntityDeath(DamageSource damageSourceForDeath)
    {
        if (api.Side != EnumAppSide.Server) return;
        ClearAllEffects();
    }
```

Notes:
- `OnEntityDeath(DamageSource)` is declared on the base class as
  `public virtual void OnEntityDeath(DamageSource damageSourceForDeath)`
  (verified in `vsapi/Common/Entity/EntityBehavior.cs` line 174).
- `DamageSource` is already imported (used elsewhere in this file —
  `OnEntityReceiveDamage` and `ApplyPeriodicEffects`).
- The clear is unconditional with respect to `Config.Enabled` — death wipes
  exposure even when the system is on. Per spec.
- Server-side gate is defensive; for player entities the death event fires
  server-authoritatively, but the gate guards against client-side mirror
  invocation in case the behavior runs on both sides.

- [ ] **Step 2: Build**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs
git commit -m "exposure: always clear state on player death

Override OnEntityDeath to wipe exposure level, condition, tier, and
stat modifiers regardless of Config.Enabled. Matches base game
behavior of resetting status conditions on death."
```

---

## Task 4: Manual verification in game

This task does not produce a commit. It validates that the three preceding
commits behave correctly end-to-end. Each scenario starts from a built mod
folder copied into the VS Mods directory (the project's debug build target
already does this — `Seafarer/bin/Debug/Mods/mod/` per `CLAUDE.md`).

- [ ] **Step 1: Confirm clean build artefact location**

```bash
ls /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/bin/Debug/Mods/mod/
```

Expected: contains `Seafarer.dll`, `modinfo.json`, `assets/`. If empty, run
the build from Task 3 Step 2 again.

- [ ] **Step 2: Scenario A — disable-clear on load**

In a survival singleplayer world:

1. Stand somewhere hot or cold long enough for `OnEnvText` to show
   "Heatstroke II" or "Frostbite II" (you can speed this by editing
   `EXPOSURE_ACCUM_RATE` higher in ConfigLib for the test).
2. Confirm a stat penalty is active — e.g. for Frostbite II, your max
   health bar is reduced by 6 HP.
3. Open the ConfigLib menu, toggle `EXPOSURE_ENABLED` off, save & exit
   to main menu.
4. Reload the same world.

Expected:
- Character info / env text no longer shows an exposure line.
- Max health bar is back to normal.
- Walk speed and other stats are normal.

If Frostbite II penalty persists after reload: the clear did not run.
Check server logs for any exception in `OnEntityLoaded` and re-verify
the override was placed in the class (not nested in another method).

- [ ] **Step 3: Scenario B — death-clear with system enabled**

1. Re-enable `EXPOSURE_ENABLED` in ConfigLib.
2. Accumulate Frostbite II again.
3. Die (e.g. fall damage, drowning, wolf).
4. Respawn at the spawn point.

Expected:
- After respawn, env text has no exposure line.
- Max health is full (no Frostbite penalty).

- [ ] **Step 4: Scenario C — death clears the heatstroke III stability offset**

1. Set `EXPOSURE_ACCUM_RATE` very high in ConfigLib so heatstroke III is
   reachable in a few minutes.
2. Reach Heatstroke III in a hot biome (env text shows "Heatstroke III").
3. Note the temporal-stability bar — it should drop by 0.5 (the
   `HeatstrokeT3StabilityOffset` value).
4. Die.

Expected:
- After respawn, the temporal-stability bar is back to its pre-heatstroke
  level. (The clear path runs `RemoveTierEffects(3)` which adds back the
  0.5 offset for heatstroke.)

- [ ] **Step 5: Scenario D — re-accumulation after a clear works**

Verifies the `lastAppliedCondition` reset in Task 1 does not break the next
condition transition.

1. With `EXPOSURE_ENABLED` on, accumulate Frostbite II.
2. Die.
3. Re-accumulate cold exposure.

Expected:
- Frostbite I, II, and III tier transitions all apply their stat penalties
  again (max health drops by the configured amount at each tier). If a tier
  transition silently fails to apply effects, the cached
  `lastAppliedCondition` was not reset on the death-clear — re-check Task 1.

- [ ] **Step 6: Note results**

If all four scenarios pass, the change is complete. If any fail, the
relevant task above lists where to look.

---

## Self-review (already performed by the planner)

- **Spec coverage:** Trigger 1 → Task 2. Trigger 2 → Task 3.
  `ClearAllEffects()` adjustment → Task 1. Manual test plan → Task 4.
- **Placeholder scan:** none.
- **Type consistency:** `ClearAllEffects()`, `LastUpdateTotalHours`,
  `lastAppliedCondition`, `EnumAppSide`, `DamageSource`, `Config.Enabled`,
  and `api.World.Calendar.TotalHours` are all referenced exactly as
  spelled in the existing file (`EntityBehaviorExposure.cs`).
- **Out of scope per spec:** mid-session live toggle response, sub-toggle
  granularity, client-side UI clearing — none of the tasks introduce them.
