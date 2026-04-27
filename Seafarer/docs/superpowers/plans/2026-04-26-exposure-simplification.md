# Exposure System Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 1s/3s/10s tri-accumulator exposure tick with a single 5-real-minute climate sample. Drop the RoomRegistry shelter check (lag source) and the rainmap skylight check.

**Architecture:** Single-file refactor of `EntityBehaviorExposure.cs`. Two semantic commits: (1) strip shelter logic, (2) change accumulation cadence to 5 real-minutes. The 10s damage tick and the heal-interception path are unchanged.

**Tech Stack:** C# 10, .NET 10, Vintage Story 1.21+ modding API. Built with `dotnet build`. No unit-test harness in this project — verification is `dotnet build` plus manual in-game testing.

**Spec:** `Seafarer/docs/superpowers/specs/2026-04-26-exposure-simplification-design.md`

---

## File Structure

Only one source file changes. No new files, no deletions, no asset/JSON changes.

- **Modify:** `Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs`

The file currently does three things in one class: tick scheduling, exposure accumulation, and tier-effect application. After this refactor it does the same three things, but the scheduling is simpler. We are not splitting the class — the file stays at roughly the same size, with the shelter-check section removed.

---

## Working Directory

All commands assume `/mnt/d/Development/vs/vsmod-seafarer` as the working directory unless otherwise stated.

The build environment variable `VINTAGE_STORY` must be set:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
```

---

## Task 1: Strip Shelter Logic

Goal: remove all room and skylight tracking. After this task, `EntityBehaviorExposure` still ticks at 1s/3s/10s but the 3s shelter tick does nothing useful and the `isSheltered` branch is gone — the player is treated as always exposed when temperature is out of range.

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs`

- [ ] **Step 1: Remove shelter state fields**

In `EntityBehaviorExposure.cs`, delete these three field declarations near the top of the class (currently around lines 18-20):

```csharp
    private bool inEnclosedRoom;
    private bool exposedToSky;
    private BlockPos plrpos = new BlockPos(0);
```

Replace with just:

```csharp
    private BlockPos plrpos = new BlockPos(0);
```

(Keep `plrpos` — it's still used by `UpdateExposure`.)

- [ ] **Step 2: Remove the slowAccum field and the 3s shelter tick from OnGameTick**

In `OnGameTick` (currently around lines 88-120), remove the `slowAccum` increment, the 3s shelter check block, and the per-frame `plrpos.Set(...)` call. The method should look like this afterward:

```csharp
    public override void OnGameTick(float deltaTime)
    {
        if (!Config.Enabled) return;

        accum += deltaTime;
        damageAccum += deltaTime;

        // Climate read and accumulation every 1 second (server only)
        if (accum > 1 && api.Side == EnumAppSide.Server)
        {
            UpdateExposure();
            accum = 0;
        }

        // Periodic effects (item drops, damage) every 10 seconds (server only)
        if (damageAccum > 10 && api.Side == EnumAppSide.Server)
        {
            ApplyPeriodicEffects();
            damageAccum = 0;
        }
    }
```

Also delete the `private float slowAccum;` field declaration near `accum` and `damageAccum`.

- [ ] **Step 3: Delete the UpdateShelterState method**

Delete the entire `UpdateShelterState()` method (currently around lines 122-128):

```csharp
    private void UpdateShelterState()
    {
        var roomRegistry = api.ModLoader.GetModSystem<Vintagestory.GameContent.RoomRegistry>();
        var room = roomRegistry.GetRoomForPosition(plrpos);
        inEnclosedRoom = room != null && (room.ExitCount == 0 || room.SkylightCount < room.NonSkylightCount);
        exposedToSky = api.World.BlockAccessor.GetRainMapHeightAt(plrpos) <= plrpos.Y;
    }
```

- [ ] **Step 4: Simplify UpdateExposure — sample plrpos here, drop isSheltered branch**

In `UpdateExposure()` (currently around lines 130-211), make these changes:

(a) Set `plrpos` at the top of the method, right after the creative/spectator guard:

```csharp
        plrpos.Set((int)entity.Pos.X, (int)(entity.Pos.Y + entity.LocalEyePos.Y * 0.5), (int)entity.Pos.Z);
        plrpos.SetDimension(entity.Pos.Dimension);
```

(b) Remove the `bool isSheltered = inEnclosedRoom || !exposedToSky;` line.

(c) Replace the entire if/else cascade that uses `isSheltered`:

```csharp
        bool isSheltered = inEnclosedRoom || !exposedToSky;

        float exposureChange = 0f;

        if (!isSheltered)
        {
            if (Config.HeatstrokeEnabled && temp >= Config.HeatThreshold)
            {
                float severity = (temp - Config.HeatThreshold) / 10f;
                exposureChange = Config.AccumulationRatePerHour * (1f + severity) * windMultiplier * hoursPassed;
                ActiveCondition = ExposureCondition.Heatstroke;
            }
            else if (Config.FrostbiteEnabled && temp <= Config.ColdThreshold)
            {
                float severity = (Config.ColdThreshold - temp) / 10f;
                exposureChange = Config.AccumulationRatePerHour * (1f + severity) * windMultiplier * hoursPassed;
                ActiveCondition = ExposureCondition.Frostbite;
            }
        }
        else if (ExposureLevel > 0)
        {
            exposureChange = -Config.DecayRatePerHour * hoursPassed;
        }
        else
        {
            // Sheltered — decay
            exposureChange = -Config.DecayRatePerHour * hoursPassed;
        }
```

with this:

```csharp
        float exposureChange;

        if (Config.HeatstrokeEnabled && temp >= Config.HeatThreshold)
        {
            float severity = (temp - Config.HeatThreshold) / 10f;
            exposureChange = Config.AccumulationRatePerHour * (1f + severity) * windMultiplier * hoursPassed;
            ActiveCondition = ExposureCondition.Heatstroke;
        }
        else if (Config.FrostbiteEnabled && temp <= Config.ColdThreshold)
        {
            float severity = (Config.ColdThreshold - temp) / 10f;
            exposureChange = Config.AccumulationRatePerHour * (1f + severity) * windMultiplier * hoursPassed;
            ActiveCondition = ExposureCondition.Frostbite;
        }
        else
        {
            exposureChange = -Config.DecayRatePerHour * hoursPassed;
        }
```

(d) Update the debug log line to drop the `sheltered` field — it's around line 197-200. Change:

```csharp
            api.Logger.Debug(
                "[Exposure] temp={0:F1} sheltered={1} frostEn={2} coldThresh={3} heatThresh={4} change={5:F6} level={6:F4}→{7:F4} cond={8}",
                temp, isSheltered, Config.FrostbiteEnabled, Config.ColdThreshold, Config.HeatThreshold,
                exposureChange, ExposureLevel, newLevel, ActiveCondition);
```

to:

```csharp
            api.Logger.Debug(
                "[Exposure] temp={0:F1} frostEn={1} coldThresh={2} heatThresh={3} change={4:F6} level={5:F4}→{6:F4} cond={7}",
                temp, Config.FrostbiteEnabled, Config.ColdThreshold, Config.HeatThreshold,
                exposureChange, ExposureLevel, newLevel, ActiveCondition);
```

- [ ] **Step 5: Build and verify it compiles**

Run from the repo root:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors. Warnings about unused variables are acceptable but should be zero for the changes in this task — if you see `'isSheltered' is assigned but never used` or similar, you missed a reference.

- [ ] **Step 6: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs
git commit -m "$(cat <<'EOF'
refactor(exposure): drop shelter and skylight checks

Removes RoomRegistry.GetRoomForPosition (the lag source) and the
rainmap skylight check. Player exposure is now purely a function of
the temperature sample at their position.

Cadence is unchanged in this commit (1s climate / 10s damage); the
5-min cadence change lands in the next commit.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Move Accumulation to 5-Real-Minute Cadence

Goal: change the climate-sample tick from every 1s to every 300s real-time, and raise the `hoursPassed` reload-jump cap from 1 game-hour to 24 game-hours.

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs`

- [ ] **Step 1: Define the cadence constant**

Add a `private const float` near the top of the class, just after the field declarations (`accum`, `damageAccum`, `plrpos`, `hodInstalled`):

```csharp
    private const float AccumulationIntervalSeconds = 300f;
```

- [ ] **Step 2: Update OnGameTick to use the new interval**

Change the `accum > 1` check in `OnGameTick` to use the constant:

```csharp
        // Climate sample and accumulation every 5 real minutes (server only)
        if (accum > AccumulationIntervalSeconds && api.Side == EnumAppSide.Server)
        {
            UpdateExposure();
            accum = 0;
        }
```

- [ ] **Step 3: Raise the hoursPassed cap from 1 to 24**

In `UpdateExposure()`, find the line that caps `hoursPassed`:

```csharp
        // Cap to prevent huge jumps after loading/reconnecting — max 1 game hour per tick
        if (hoursPassed > 1f) hoursPassed = 1f;
```

Replace with:

```csharp
        // Cap to prevent huge jumps after loading/reconnecting — max 24 game hours per tick.
        // 5 real minutes is ~13 game-hours at default day length, so 24 leaves headroom for
        // longer day-length configs while still capping the first tick after a long offline.
        if (hoursPassed > 24f) hoursPassed = 24f;
```

- [ ] **Step 4: Update the debug-log throttle**

The debug log inside `UpdateExposure()` is gated by `damageAccum > 9.5f` so it fires roughly once per 10s. With the new 5-min cadence, `UpdateExposure` itself fires once per 5 min, so the throttle is no longer meaningful. Remove the throttle so every accumulation tick logs:

Find:

```csharp
        if (exposureChange != 0 && damageAccum > 9.5f)
        {
            api.Logger.Debug(...);
        }
```

Replace with:

```csharp
        if (exposureChange != 0)
        {
            api.Logger.Debug(...);
        }
```

(Keep the format string from Task 1 Step 4d — the one without the `sheltered` field.)

- [ ] **Step 5: Build and verify it compiles**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorExposure.cs
git commit -m "$(cat <<'EOF'
perf(exposure): sample temperature every 5 real minutes

Replaces the 1-second climate-read tick with a 300-second tick. Damage
tick stays at 10s. Raises the hoursPassed clamp from 1 to 24 game-hours
since 5 real-minutes is ~13 game-hours at default day length and the
clamp now exists only to bound first-tick-after-load jumps.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: In-Game Manual Verification

Goal: confirm the refactor behaves correctly under real gameplay. There is no automated test harness — this is the only functional verification.

**Files:** none (game testing only)

- [ ] **Step 1: Launch the game with the dev mod loaded**

Use the standard dev-launch flow for this project (the existing run configuration with `--addOrigin` pointing at `Seafarer/Seafarer/bin/Debug/Mods/mod`).

- [ ] **Step 2: Verify normal-temperature decay**

Start in temperate biome with `ExposureLevel == 0` (default). Open `/exposurelevel set 0.5` if such a debug command exists, otherwise edit a save where exposure is mid-range. Wait at least 5 real minutes. Confirm via debug log that:
- A single `[Exposure]` log line appears every 5 minutes (not every second).
- Exposure level decays slowly toward 0.

- [ ] **Step 3: Verify heatstroke accumulation**

Travel to a hot biome (or use a temporal-stability scepter / cheat to teleport). Stay there for 10+ real minutes. Confirm:
- Two `[Exposure]` log lines have appeared (every 5 min).
- `ExposureLevel` is rising.
- `ActiveCondition` is `Heatstroke` (1).
- Once tier crosses thresholds, walkspeed/healing penalties apply.

- [ ] **Step 4: Verify the room-no-longer-protects behavior**

Build a fully enclosed shelter inside the hot biome and stand inside it. Wait 10+ real minutes. Confirm:
- Exposure continues to rise (or holds, depending on indoor temperature reported by the climate engine).
- Previously this would have decayed; the design accepts this trade-off.

- [ ] **Step 5: Verify damage tick still responsive at Tier 3**

Force a high `ExposureLevel` (≥ `Tier3Threshold` = 1.0) via debug or by waiting it out. Confirm:
- HP decrements every ~10 seconds (via `ApplyPeriodicEffects`).
- Stat penalties applied.

- [ ] **Step 6: Verify healing still reduces exposure immediately**

At Tier 1+ exposure, eat a healing item or use a poultice. Confirm:
- `ExposureLevel` drops by `healAmount * HealingExposureReduction` immediately.
- Tier effects update without waiting for the 5-min boundary.

- [ ] **Step 7: Verify no lag spike**

With the in-game F3 debug overlay open, watch the server tick-time graph for 5+ minutes in a hot biome. Confirm:
- No periodic 3-second-interval spikes (the previous `RoomRegistry` cost).
- Tick time remains stable.

- [ ] **Step 8: If all checks pass, mark the task complete**

No commit needed — testing only.

---

## Self-Review

After writing this plan, I checked:

**Spec coverage** — every spec section has a task:
- "Tick cadence" → Task 2 Steps 1-2.
- "State removed" (`inEnclosedRoom`, `exposedToSky`, `UpdateShelterState`, `isSheltered`) → Task 1 Steps 1-4.
- "Simplified flow" steps 1-8 → Task 1 Step 4 + Task 2 Step 3.
- "Damage tick unchanged" → not modified in either task; verified in Task 3 Step 5.
- "Healing interception unchanged" → not modified; verified in Task 3 Step 6.
- "hoursPassed cap raised to 24" → Task 2 Step 3.
- Performance impact → verified in Task 3 Step 7.
- Behavior changes for players (shelter no longer protects, stat-penalty latency, damage latency, first-tick-after-load) → verified in Task 3 Steps 4 and 5.

**Placeholder scan** — no TBD/TODO/"add appropriate" patterns. Every code step shows the actual code.

**Type consistency** — the only new symbol is `AccumulationIntervalSeconds` (a `const float`), used in one place. `damageAccum`, `accum`, `plrpos`, `UpdateExposure`, `ApplyPeriodicEffects`, `Config.*` properties all match their definitions in the existing source.
