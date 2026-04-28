# Boat & Raft Health Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give all four target boats (seafarer outrigger, seafarer logbarge, vanilla raft, vanilla sailed) HP, combat/collision/storm damage, repair via glue or marine varnish, and wreckage drops on death — all driven from a single `attributes.extendShipMechanics` JSON block.

**Architecture:** One new `EntityBehaviorShipMechanics` (server + client) lives alongside the standard `EntityBehaviorHealth`. It overwrites the sibling health behavior's `BaseMaxHealth`/`Health` from config in `AfterInitialized`, runs a 1 Hz server-only tick for collision and storm damage, and hooks `OnEntityDeath` for wreckage drops. One new `CollectibleBehaviorBoatRepair` is patched onto repair items; it drains the held liquid container and heals the targeted boat. Vanilla boats and items are wired up via JSON patches.

**Tech Stack:** C# 10, .NET 10, Vintage Story 1.21+ modding API. Built with `dotnet build`. No unit-test harness in this project — verification is `dotnet build` plus the manual playtest checklist at the end.

**Spec:** `Seafarer/docs/superpowers/specs/2026-04-28-boat-raft-health-design.md`

---

## File Structure

**New files:**

- `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs` — the boat-side behavior. One class, one responsibility (translate the `extendShipMechanics` config into HP setup, damage ticks, and death drops).
- `Seafarer/Seafarer/CollectibleBehavior/BehaviorBoatRepair.cs` — the item-side behavior. One class. Reads its own per-item `hpPerLitre`/`litresPerUse` from the JSON behavior config; doesn't know which item it's attached to.
- `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-raft-shipmechanics.json` — JSON patch that adds health + shipmechanics behaviors and the config block to vanilla `boat-raft`.
- `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-sailed-shipmechanics.json` — same for vanilla `boat-sailed`.
- `Seafarer/Seafarer/assets/seafarer/patches/glueportion-boatrepair.json` — patches the vanilla glue portion to add the boatrepair behavior.
- `Seafarer/Seafarer/assets/seafarer/patches/varnishportion-marine-boatrepair.json` — same for the seafarer marine varnish portion.

**Modified files:**

- `Seafarer/Seafarer/SeafarerModSystem.cs` — registers the two new behavior classes.
- `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-outrigger.json` — adds `health` + `shipmechanics` behaviors and the `extendShipMechanics` block.
- `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-logbarge.json` — same.
- `Seafarer/Seafarer/assets/seafarer/lang/en.json` — adds repair UI/error strings.

No existing file's responsibilities change. The two new classes have a single purpose each. Both behavior classes follow the existing patterns established by `EntityBehaviorExposure.cs` and `BehaviorPlaceBurrito.cs`.

---

## Working Directory

All commands assume `/mnt/d/Development/vs/vsmod-seafarer` as the working directory unless otherwise stated.

The build environment variable `VINTAGE_STORY` must be set:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
```

---

## Task 1: Create `EntityBehaviorShipMechanics` Skeleton + Register It

Goal: a stub behavior class compiles, registers under code `shipmechanics`, and can be added to a boat JSON without breaking the build. No HP logic yet.

**Files:**
- Create: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs`
- Modify: `Seafarer/Seafarer/SeafarerModSystem.cs`

- [ ] **Step 1: Create the new behavior file**

Write `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs`:

```csharp
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Seafarer;

public class EntityBehaviorShipMechanics : EntityBehavior
{
    public const string Code = "shipmechanics";

    private ICoreAPI api = null!;
    private JsonObject? cfg;

    public EntityBehaviorShipMechanics(Entity entity) : base(entity) { }

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
    {
        api = entity.World.Api;
        cfg = entity.Properties.Attributes?["extendShipMechanics"];
    }
}
```

- [ ] **Step 2: Register the behavior class in the mod system**

In `Seafarer/Seafarer/SeafarerModSystem.cs`, find the line that registers `seafarer:exposure` (around line 53):

```csharp
            api.RegisterEntityBehaviorClass("seafarer:exposure", typeof(EntityBehaviorExposure));
```

Add immediately after it:

```csharp
            api.RegisterEntityBehaviorClass("shipmechanics", typeof(EntityBehaviorShipMechanics));
```

- [ ] **Step 3: Build to verify it compiles**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs Seafarer/Seafarer/SeafarerModSystem.cs
git commit -m "$(cat <<'EOF'
feat(boats): scaffold EntityBehaviorShipMechanics

Empty behavior registered under code "shipmechanics". Reads the
extendShipMechanics attributes block in Initialize but does
nothing with it yet. Subsequent commits add HP wiring, damage,
and death drops.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Wire HP from Config Into the Standard `health` Behavior

Goal: when `extendShipMechanics.health = N` is present, the entity's standard `EntityBehaviorHealth` reports `MaxHealth = N` and starts at full HP.

This uses `AfterInitialized(bool onFirstSpawn)` — that's the engine's documented hook for cross-behavior setup, called after every behavior's `Initialize` has run. We update `BaseMaxHealth` and `Health` directly on the sibling behavior, then call `UpdateMaxHealth()` to recompute the cached value.

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs`

- [ ] **Step 1: Add the HP-sync override**

Append to the class body in `EntityBehaviorShipMechanics.cs`, after `Initialize`:

```csharp
    public override void AfterInitialized(bool onFirstSpawn)
    {
        if (cfg == null || !cfg.KeyExists("health")) return;

        float configuredHealth = cfg["health"].AsFloat(-1f);
        if (configuredHealth <= 0f) return;

        var healthBh = entity.GetBehavior<EntityBehaviorHealth>();
        if (healthBh == null)
        {
            api.Logger.Warning(
                "[seafarer] shipmechanics: entity {0} has extendShipMechanics but no health behavior; HP not applied.",
                entity.Code);
            return;
        }

        healthBh.BaseMaxHealth = configuredHealth;
        healthBh.UpdateMaxHealth();
        if (onFirstSpawn)
        {
            healthBh.Health = healthBh.MaxHealth;
        }
    }
```

- [ ] **Step 2: Build to verify it compiles**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs
git commit -m "$(cat <<'EOF'
feat(boats): apply configured HP via standard health behavior

In AfterInitialized, write extendShipMechanics.health into the
sibling EntityBehaviorHealth's BaseMaxHealth and refresh the
cached MaxHealth. On first spawn the boat starts at full HP;
on reload existing health is preserved. Logs a warning if the
entity has the config block but no health behavior.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Implement Collision Damage in `OnGameTick`

Goal: when a boat slams into terrain at speed (above `collision.minSpeed`), it takes a single damage tick proportional to `(speed − minSpeed) × damagePerSpeedUnit`. A cooldown prevents one crash from registering as 20.

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs`

- [ ] **Step 1: Add fields for tick state and config defaults**

Add these private fields immediately after the `cfg` field at the top of the class:

```csharp
    private const float TickIntervalSeconds = 1.0f;

    private float tickAccum;
    private float collisionCooldown;
    private double prevSpeed;

    private float collisionMinSpeed = 0.30f;
    private float collisionDamagePerSpeedUnit = 8.0f;
    private float collisionCooldownSeconds = 1.0f;
```

- [ ] **Step 2: Read collision config in `Initialize`**

Replace the body of `Initialize` (after the existing two lines) so it reads the optional `collision` sub-block:

```csharp
    public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
    {
        api = entity.World.Api;
        cfg = entity.Properties.Attributes?["extendShipMechanics"];

        if (cfg != null && cfg.KeyExists("collision"))
        {
            var c = cfg["collision"];
            collisionMinSpeed = c["minSpeed"].AsFloat(collisionMinSpeed);
            collisionDamagePerSpeedUnit = c["damagePerSpeedUnit"].AsFloat(collisionDamagePerSpeedUnit);
            collisionCooldownSeconds = c["cooldownSeconds"].AsFloat(collisionCooldownSeconds);
        }
    }
```

- [ ] **Step 3: Add the `OnGameTick` override**

Append after `AfterInitialized`:

```csharp
    public override void OnGameTick(float deltaTime)
    {
        if (api.Side != EnumAppSide.Server) return;

        if (collisionCooldown > 0f) collisionCooldown -= deltaTime;

        // Sample current horizontal speed every frame so we can detect
        // the impact tick. Storing prevSpeed lets us deal damage based on
        // the speed *before* the wall stopped us.
        var v = entity.SidedPos.Motion;
        double horizSpeed = Math.Sqrt(v.X * v.X + v.Z * v.Z);

        if (entity.CollidedHorizontally
            && collisionCooldown <= 0f
            && prevSpeed > collisionMinSpeed)
        {
            float damage = (float)(prevSpeed - collisionMinSpeed) * collisionDamagePerSpeedUnit;
            if (damage > 0f)
            {
                entity.ReceiveDamage(
                    new DamageSource { Source = EnumDamageSource.Block, Type = EnumDamageType.Crushing },
                    damage);
                collisionCooldown = collisionCooldownSeconds;
            }
        }

        prevSpeed = horizSpeed;

        // Periodic (storm) work goes here in Task 4 — leave the accumulator wired up.
        tickAccum += deltaTime;
        if (tickAccum < TickIntervalSeconds) return;
        tickAccum = 0f;
    }
```

- [ ] **Step 4: Build to verify it compiles**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs
git commit -m "$(cat <<'EOF'
feat(boats): apply crushing damage on high-speed collisions

OnGameTick samples horizontal speed each frame on the server.
When CollidedHorizontally fires and the prior-frame speed was
above minSpeed, damage = (speed − minSpeed) × damagePerSpeedUnit
is applied as Block/Crushing. A cooldown prevents one crash
from registering more than once.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Implement Storm Damage in `OnGameTick`

Goal: while wind speed at the boat's position is above `storm.minWindSpeed` and (optionally) the boat is in deep water, slowly drain HP.

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs`

- [ ] **Step 1: Add storm config fields and defaults**

Append these fields under the existing collision-config fields:

```csharp
    private float stormMinWindSpeed = 0.65f;
    private float stormDamagePerSecond = 0.4f;
    private bool stormRequiresDeepWater = true;
```

- [ ] **Step 2: Read the storm sub-block in `Initialize`**

Inside `Initialize`, after the collision block, append:

```csharp
        if (cfg != null && cfg.KeyExists("storm"))
        {
            var s = cfg["storm"];
            stormMinWindSpeed = s["minWindSpeed"].AsFloat(stormMinWindSpeed);
            stormDamagePerSecond = s["damagePerSecond"].AsFloat(stormDamagePerSecond);
            stormRequiresDeepWater = s["requiresDeepWater"].AsBool(stormRequiresDeepWater);
        }
```

- [ ] **Step 3: Replace the placeholder periodic block in `OnGameTick`**

In `OnGameTick`, replace the comment/block that currently reads:

```csharp
        // Periodic (storm) work goes here in Task 4 — leave the accumulator wired up.
        tickAccum += deltaTime;
        if (tickAccum < TickIntervalSeconds) return;
        tickAccum = 0f;
```

with:

```csharp
        tickAccum += deltaTime;
        if (tickAccum < TickIntervalSeconds) return;
        float stormDelta = tickAccum;
        tickAccum = 0f;

        ApplyStormDamage(stormDelta);
```

- [ ] **Step 4: Add the `ApplyStormDamage` helper**

Append after `OnGameTick`:

```csharp
    private void ApplyStormDamage(float intervalSeconds)
    {
        if (stormDamagePerSecond <= 0f || stormMinWindSpeed > 1f) return;

        var pos = entity.ServerPos.AsBlockPos;
        var wind = api.World.BlockAccessor.GetWindSpeedAt(pos);
        double windMag = Math.Sqrt(wind.X * wind.X + wind.Y * wind.Y + wind.Z * wind.Z);
        if (windMag < stormMinWindSpeed) return;

        if (stormRequiresDeepWater && !IsOverDeepWater(pos)) return;

        float damage = stormDamagePerSecond * intervalSeconds;
        entity.ReceiveDamage(
            new DamageSource { Source = EnumDamageSource.Void, Type = EnumDamageType.Other },
            damage);
    }

    private bool IsOverDeepWater(BlockPos boatPos)
    {
        // Sample three blocks below the boat. All must be liquid for "deep water".
        var probe = boatPos.Copy();
        for (int i = 0; i < 3; i++)
        {
            var block = api.World.BlockAccessor.GetBlock(probe);
            if (block.LiquidCode == null) return false;
            probe.Y--;
        }
        return true;
    }
```

- [ ] **Step 5: Build to verify it compiles**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs
git commit -m "$(cat <<'EOF'
feat(boats): apply storm damage in deep water during high winds

Adds a 1 Hz server-side ApplyStormDamage that samples wind
speed at the boat position and probes the three blocks beneath
for liquid. When both checks pass, damagePerSecond × interval
is applied as Void/Other so it isn't attributed to any agent.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Wreckage Drop on `OnEntityDeath`

Goal: when the boat dies, spawn a fraction of its `deconstructDrops` items at the death position. Items get a small upward velocity if `dropFloating` is true so they bob on the water surface.

The boat JSONs already have `deconstructDrops` (or `deconstructDropsByType` for logbarge). Variant placeholders like `{material}` are substituted at entity-load time, so by the time we read `entity.Properties.Attributes`, those strings are already resolved.

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs`

- [ ] **Step 1: Add wreckage config fields and defaults**

Append these fields under the existing storm-config fields:

```csharp
    private float wreckageDropFraction = 0.4f;
    private bool wreckageDropFloating = true;
```

- [ ] **Step 2: Read the wreckage sub-block in `Initialize`**

Inside `Initialize`, after the storm block, append:

```csharp
        if (cfg != null && cfg.KeyExists("wreckage"))
        {
            var w = cfg["wreckage"];
            wreckageDropFraction = w["dropFraction"].AsFloat(wreckageDropFraction);
            wreckageDropFloating = w["dropFloating"].AsBool(wreckageDropFloating);
        }
```

- [ ] **Step 3: Override `OnEntityDeath`**

Append after `IsOverDeepWater`:

```csharp
    public override void OnEntityDeath(DamageSource damageSourceForDeath)
    {
        if (api.Side != EnumAppSide.Server) return;
        if (wreckageDropFraction <= 0f) return;

        var attrs = entity.Properties.Attributes;
        if (attrs == null) return;

        // Boats use either "deconstructDrops" or "deconstructDropsByType".
        // Resolve to a flat array using the entity's full code.
        JsonObject? dropsArray = null;
        if (attrs.KeyExists("deconstructDrops"))
        {
            dropsArray = attrs["deconstructDrops"];
        }
        else if (attrs.KeyExists("deconstructDropsByType"))
        {
            // JsonObject doesn't expose key iteration — drop down to the
            // underlying JObject to walk wildcard keys like "boat-raft-*".
            var byTypeJObj = attrs["deconstructDropsByType"].Token as Newtonsoft.Json.Linq.JObject;
            if (byTypeJObj != null)
            {
                string code = entity.Code.ToString();
                foreach (var prop in byTypeJObj.Properties())
                {
                    if (WildcardUtil.Match(prop.Name, code))
                    {
                        dropsArray = new JsonObject(prop.Value);
                        break;
                    }
                }
            }
        }

        if (dropsArray == null || !dropsArray.IsArray()) return;

        var drops = dropsArray.AsArray();
        if (drops == null) return;
        var rand = api.World.Rand;
        var spawnPos = entity.ServerPos.XYZ.AddCopy(0, 0.1, 0);

        foreach (var drop in drops)
        {
            string code = drop["code"].AsString();
            string type = drop["type"].AsString("item");
            int quantity = drop["quantity"].AsInt(1);
            if (string.IsNullOrEmpty(code) || quantity <= 0) continue;

            float scaled = quantity * wreckageDropFraction;
            int count = (int)Math.Floor(scaled);
            float remainder = scaled - count;
            if (remainder > 0f && rand.NextDouble() < remainder) count++;
            if (count <= 0) continue;

            ItemStack? stack = ResolveStack(type, code, count);
            if (stack == null) continue;

            var velocity = wreckageDropFloating
                ? new Vec3f((float)(rand.NextDouble() - 0.5) * 0.1f, 0.1f, (float)(rand.NextDouble() - 0.5) * 0.1f)
                : null;

            api.World.SpawnItemEntity(stack, spawnPos, velocity);
        }
    }

    private ItemStack? ResolveStack(string type, string code, int quantity)
    {
        var loc = new AssetLocation(code);
        if (type == "block")
        {
            var block = api.World.GetBlock(loc);
            return block == null ? null : new ItemStack(block, quantity);
        }
        var item = api.World.GetItem(loc);
        return item == null ? null : new ItemStack(item, quantity);
    }
```

- [ ] **Step 4: Build to verify it compiles**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs
git commit -m "$(cat <<'EOF'
feat(boats): drop fractional wreckage on death

OnEntityDeath reads the entity's deconstructDrops (or the
matched entry from deconstructDropsByType) and spawns each
stack scaled by wreckage.dropFraction. Fractional remainders
promote to one extra item probabilistically. Items get a
small upward velocity when dropFloating is true so they bob
on the surface instead of sinking.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Create `BehaviorBoatRepair` (Item-Side) + Register It

Goal: a `CollectibleBehavior` that, when the player right-click-and-holds the patched item on an entity that has `EntityBehaviorShipMechanics`, drains liquid from the held container and heals the boat.

**Files:**
- Create: `Seafarer/Seafarer/CollectibleBehavior/BehaviorBoatRepair.cs`
- Modify: `Seafarer/Seafarer/SeafarerModSystem.cs`

- [ ] **Step 1: Create the file**

Write `Seafarer/Seafarer/CollectibleBehavior/BehaviorBoatRepair.cs`:

```csharp
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

public class BehaviorBoatRepair : CollectibleBehavior
{
    public const string Code = "boatrepair";

    private const float UseDurationSeconds = 1.5f;

    private float hpPerLitre = 20f;
    private float litresPerUse = 0.25f;

    public BehaviorBoatRepair(CollectibleObject collObj) : base(collObj) { }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        hpPerLitre = properties["hpPerLitre"].AsFloat(hpPerLitre);
        litresPerUse = properties["litresPerUse"].AsFloat(litresPerUse);
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling,
        ref EnumHandling handling)
    {
        if (entitySel?.Entity == null) return;
        var ship = entitySel.Entity.GetBehavior<EntityBehaviorShipMechanics>();
        if (ship == null) return;

        var healthBh = entitySel.Entity.GetBehavior<EntityBehaviorHealth>();
        if (healthBh == null) return;

        if (healthBh.Health >= healthBh.MaxHealth)
        {
            if (byEntity is EntityPlayer entityPlayer && byEntity.World.Side == EnumAppSide.Client)
            {
                (entityPlayer.Player as IClientPlayer)?.ShowChatNotification(
                    Lang.Get("seafarer:boatrepair-fullhp"));
            }
            handling = EnumHandling.PreventSubsequent;
            handHandling = EnumHandHandling.PreventDefault;
            return;
        }

        if (GetAvailableLitres(slot) < litresPerUse) return;

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;

        if (byEntity.World.Side == EnumAppSide.Client)
        {
            byEntity.World.PlaySoundAt(
                new AssetLocation("game:sounds/player/gluerepair" + ((byEntity.World.Rand.Next(4)) + 1)),
                byEntity, byEntity is EntityPlayer ep ? ep.Player : null);
        }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (entitySel?.Entity == null) return false;
        if (entitySel.Entity.GetBehavior<EntityBehaviorShipMechanics>() == null) return false;

        handling = EnumHandling.PreventSubsequent;
        return secondsUsed < UseDurationSeconds;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (secondsUsed < UseDurationSeconds - 0.05f) return;
        if (byEntity.World.Side != EnumAppSide.Server) return;
        if (entitySel?.Entity == null) return;

        var ship = entitySel.Entity.GetBehavior<EntityBehaviorShipMechanics>();
        if (ship == null) return;
        var healthBh = entitySel.Entity.GetBehavior<EntityBehaviorHealth>();
        if (healthBh == null) return;
        if (healthBh.Health >= healthBh.MaxHealth) return;

        if (!TryDrainLitres(slot, byEntity, litresPerUse)) return;

        float restored = hpPerLitre * litresPerUse;
        healthBh.Health = System.Math.Min(healthBh.Health + restored, healthBh.MaxHealth);
        entitySel.Entity.WatchedAttributes.MarkPathDirty("health");

        handling = EnumHandling.PreventSubsequent;
    }

    private float GetAvailableLitres(ItemSlot slot)
    {
        var stack = slot.Itemstack;
        if (stack?.Collectible is BlockLiquidContainerBase container)
        {
            var content = container.GetContent(stack);
            if (content == null) return 0f;
            var props = BlockLiquidContainerBase.GetContainableProps(content);
            if (props == null) return 0f;
            return content.StackSize / props.ItemsPerLitre;
        }
        return 0f;
    }

    private bool TryDrainLitres(ItemSlot slot, EntityAgent byEntity, float litres)
    {
        var stack = slot.Itemstack;
        if (stack?.Collectible is not BlockLiquidContainerBase container) return false;

        var content = container.GetContent(stack);
        if (content == null) return false;
        var props = BlockLiquidContainerBase.GetContainableProps(content);
        if (props == null) return false;

        int needed = (int)System.Math.Ceiling(litres * props.ItemsPerLitre);
        if (content.StackSize < needed) return false;

        content.StackSize -= needed;
        if (content.StackSize <= 0)
        {
            container.SetContent(stack, null);
        }
        else
        {
            container.SetContent(stack, content);
        }
        slot.MarkDirty();
        return true;
    }
}
```

- [ ] **Step 2: Register the behavior class**

In `Seafarer/Seafarer/SeafarerModSystem.cs`, find the existing `RegisterCollectibleBehaviorClass` calls (around lines 47-49):

```csharp
            api.RegisterCollectibleBehaviorClass("PlaceBurrito", typeof(BehaviorPlaceBurrito));
            api.RegisterCollectibleBehaviorClass("CoconutCrack", typeof(BehaviorCoconutCrack));
```

Add immediately after the second one:

```csharp
            api.RegisterCollectibleBehaviorClass("boatrepair", typeof(BehaviorBoatRepair));
```

- [ ] **Step 3: Build to verify it compiles**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/CollectibleBehavior/BehaviorBoatRepair.cs Seafarer/Seafarer/SeafarerModSystem.cs
git commit -m "$(cat <<'EOF'
feat(boats): item-side BehaviorBoatRepair for in-world repair

Patched onto liquid-container repair items (glue, marine
varnish). On a 1.5s right-click-hold against an entity with
EntityBehaviorShipMechanics: drains litresPerUse from the
held container and heals the boat by hpPerLitre × litresPerUse.
Blocks the action with a fail message at full HP.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Wire Seafarer's Two Boats

Goal: add the standard `health` behavior, the new `shipmechanics` behavior, and the `extendShipMechanics` attribute block to both seafarer boat JSONs. Outrigger uses `healthByType` so seasoned and varnished have different HP.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-outrigger.json`
- Modify: `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-logbarge.json`

- [ ] **Step 1: Add behaviors and config to `boat-outrigger.json`**

In `boat-outrigger.json`, locate the `client.behaviors` array (currently ends with `{ "code": "creaturecarrier" }`) and add two entries to the end:

```json
      { "code": "creaturecarrier" },
      { "code": "health" },
      { "code": "shipmechanics" }
```

Do the same for `server.behaviors`. Both lists must end with these two new entries.

Then locate the `attributes` block. After the closing brace of `mountAnimations` (and the comma that terminates the `mountAnimations` entry), add the new field. The current attributes object ends with:

```json
    "mountAnimations": {
      "idle": "sitboatidle",
      "ready": "",
      "forwards": "",
      "backwards": ""
    }
  },
```

Replace the closing `}` of `mountAnimations` and the trailing comma sequence with:

```json
    "mountAnimations": {
      "idle": "sitboatidle",
      "ready": "",
      "forwards": "",
      "backwards": ""
    },
    "extendShipMechanicsByType": {
      "*-varnished": {
        "health": 280,
        "collision": { "minSpeed": 0.30, "damagePerSpeedUnit": 8.0, "cooldownSeconds": 1.0 },
        "storm": { "minWindSpeed": 0.65, "damagePerSecond": 0.4, "requiresDeepWater": true },
        "wreckage": { "dropFraction": 0.4, "dropFloating": true }
      },
      "*-seasoned": {
        "health": 200,
        "collision": { "minSpeed": 0.30, "damagePerSpeedUnit": 8.0, "cooldownSeconds": 1.0 },
        "storm": { "minWindSpeed": 0.65, "damagePerSecond": 0.4, "requiresDeepWater": true },
        "wreckage": { "dropFraction": 0.4, "dropFloating": true }
      }
    }
  },
```

(The Vintage Story entity loader resolves `*ByType` keys against the entity's full code at load time, so the runtime attribute key becomes `extendShipMechanics`.)

- [ ] **Step 2: Add behaviors and config to `boat-logbarge.json`**

Append `{ "code": "health" }` and `{ "code": "shipmechanics" }` to both `client.behaviors` and `server.behaviors`, after `creaturecarrier`, same as outrigger.

In the `attributes` block (currently ends after `mountAnimations`), add:

```json
    "extendShipMechanics": {
      "health": 100,
      "collision": { "minSpeed": 0.30, "damagePerSpeedUnit": 8.0, "cooldownSeconds": 1.0 },
      "storm": { "minWindSpeed": 0.65, "damagePerSecond": 0.4, "requiresDeepWater": true },
      "wreckage": { "dropFraction": 0.5, "dropFloating": true }
    }
```

(Logbarge has only one variant so no `*ByType` form is needed.)

- [ ] **Step 3: Build to verify the JSON parses**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors. (JSON syntax errors don't surface here, but compilation validates the C# half.)

- [ ] **Step 4: Run the asset validator**

The seafarer mod has a `validate-mod-assets` skill — invoke it via the Skill tool with `args: "boat-outrigger.json boat-logbarge.json"` so it focuses on the changed files.

Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-outrigger.json Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-logbarge.json
git commit -m "$(cat <<'EOF'
feat(boats): wire health + shipmechanics on seafarer vessels

Adds the standard health behavior, new shipmechanics behavior,
and per-variant extendShipMechanics config to boat-outrigger
(280 HP varnished / 200 HP seasoned) and boat-logbarge (100 HP).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Patch Vanilla `boat-raft` and `boat-sailed`

Goal: two JSON-Patch files in `assets/seafarer/patches/` add the same behaviors and config to the vanilla boats.

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-raft-shipmechanics.json`
- Create: `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-sailed-shipmechanics.json`

- [ ] **Step 1: Create the raft patch**

Write `vanilla-boat-raft-shipmechanics.json`:

```json
[
  {
    "op": "add",
    "path": "/server/behaviors/-",
    "value": { "code": "health" },
    "file": "game:entities/nonliving/boat-raft"
  },
  {
    "op": "add",
    "path": "/server/behaviors/-",
    "value": { "code": "shipmechanics" },
    "file": "game:entities/nonliving/boat-raft"
  },
  {
    "op": "add",
    "path": "/client/behaviors/-",
    "value": { "code": "health" },
    "file": "game:entities/nonliving/boat-raft"
  },
  {
    "op": "add",
    "path": "/client/behaviors/-",
    "value": { "code": "shipmechanics" },
    "file": "game:entities/nonliving/boat-raft"
  },
  {
    "op": "add",
    "path": "/attributes/extendShipMechanics",
    "value": {
      "health": 60,
      "collision": { "minSpeed": 0.30, "damagePerSpeedUnit": 8.0, "cooldownSeconds": 1.0 },
      "storm": { "minWindSpeed": 0.65, "damagePerSecond": 0.4, "requiresDeepWater": true },
      "wreckage": { "dropFraction": 0.5, "dropFloating": true }
    },
    "file": "game:entities/nonliving/boat-raft"
  }
]
```

- [ ] **Step 2: Create the sailed-boat patch**

Write `vanilla-boat-sailed-shipmechanics.json`:

```json
[
  {
    "op": "add",
    "path": "/server/behaviors/-",
    "value": { "code": "health" },
    "file": "game:entities/nonliving/boat-sailed"
  },
  {
    "op": "add",
    "path": "/server/behaviors/-",
    "value": { "code": "shipmechanics" },
    "file": "game:entities/nonliving/boat-sailed"
  },
  {
    "op": "add",
    "path": "/client/behaviors/-",
    "value": { "code": "health" },
    "file": "game:entities/nonliving/boat-sailed"
  },
  {
    "op": "add",
    "path": "/client/behaviors/-",
    "value": { "code": "shipmechanics" },
    "file": "game:entities/nonliving/boat-sailed"
  },
  {
    "op": "add",
    "path": "/attributes/extendShipMechanics",
    "value": {
      "health": 120,
      "collision": { "minSpeed": 0.30, "damagePerSpeedUnit": 8.0, "cooldownSeconds": 1.0 },
      "storm": { "minWindSpeed": 0.65, "damagePerSecond": 0.4, "requiresDeepWater": true },
      "wreckage": { "dropFraction": 0.4, "dropFloating": true }
    },
    "file": "game:entities/nonliving/boat-sailed"
  }
]
```

- [ ] **Step 3: Run the asset validator**

Invoke the `validate-mod-assets` skill on the new patch files.

Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-raft-shipmechanics.json Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-sailed-shipmechanics.json
git commit -m "$(cat <<'EOF'
feat(boats): patch vanilla raft and sailed boat with health system

Two JSON patches add the standard health behavior, the new
shipmechanics behavior, and the extendShipMechanics config
block to game:entities/nonliving/boat-raft (60 HP) and
boat-sailed (120 HP).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Patch Glue and Marine Varnish Items

Goal: each repair item gets `{ "code": "boatrepair", ... }` added to its behaviors list. Vanilla glue starts with no behaviors array, so the patch creates it; the seafarer varnish item also starts without one.

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/patches/glueportion-boatrepair.json`
- Create: `Seafarer/Seafarer/assets/seafarer/patches/varnishportion-marine-boatrepair.json`

- [ ] **Step 1: Create the glue patch**

Write `glueportion-boatrepair.json`:

```json
[
  {
    "op": "addmerge",
    "path": "/behaviors",
    "value": [
      { "code": "boatrepair", "hpPerLitre": 20.0, "litresPerUse": 0.25 }
    ],
    "file": "game:itemtypes/liquid/glueportion"
  }
]
```

(`addmerge` creates the array if absent and appends to it if present — VS-flavored JSON-Patch op.)

- [ ] **Step 2: Create the marine varnish patch**

Write `varnishportion-marine-boatrepair.json`:

```json
[
  {
    "op": "addmerge",
    "path": "/behaviors",
    "value": [
      { "code": "boatrepair", "hpPerLitre": 50.0, "litresPerUse": 0.25 }
    ],
    "file": "seafarer:itemtypes/liquid/varnishportion-marine"
  }
]
```

- [ ] **Step 3: Run the asset validator**

Invoke the `validate-mod-assets` skill on the new patch files.

Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/patches/glueportion-boatrepair.json Seafarer/Seafarer/assets/seafarer/patches/varnishportion-marine-boatrepair.json
git commit -m "$(cat <<'EOF'
feat(boats): patch glue and marine varnish with boatrepair

Patches add the boatrepair CollectibleBehavior to the vanilla
glueportion and seafarer varnishportion-marine items. Glue
restores 5 HP per use; varnish restores 12.5 HP per use.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Add Lang Strings for the Repair Fail Message

Goal: the `seafarer:boatrepair-fullhp` lang key resolves to a readable string.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/lang/en.json`

- [ ] **Step 1: Add the lang entry**

In `en.json`, the file currently ends with this (around the last 10 lines):

```json
  "creature-seafarer:outrigger-construction-*-selectionbox-SeleAP": "Outrigger (under construction)"
}
```

Replace those final lines with:

```json
  "creature-seafarer:outrigger-construction-*-selectionbox-SeleAP": "Outrigger (under construction)",
  "seafarer:boatrepair-fullhp": "This boat is already in perfect condition."
}
```

- [ ] **Step 2: Run the asset validator**

Invoke the `validate-mod-assets` skill.

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/lang/en.json
git commit -m "$(cat <<'EOF'
feat(boats): lang string for full-HP repair fail message

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 11: Manual Playtest

Goal: confirm the system works end-to-end in-game before declaring the feature complete. No automated tests exist for this codebase; this checklist is the verification.

**Build and launch:**

- [ ] Build the mod:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

- [ ] Launch the game with the mod and load a creative test world over deep ocean.

**Per-boat HP and combat damage:**

- [ ] `/entity spawn boat-raft` → press F3, locate the entity, verify health line shows `60/60`. Hit it with a sword → HP decreases. Kill it with arrows from full HP.
- [ ] `/entity spawn boat-sailed` → verify `120/120`. Repeat damage test.
- [ ] `/entity spawn boat-logbarge` → verify `100/100`. Repeat.
- [ ] `/entity spawn boat-outrigger-seasoned` → verify `200/200`. Repeat.
- [ ] `/entity spawn boat-outrigger-varnished` → verify `280/280`. Repeat.

**Collision damage:**

- [ ] Mount a sailed-boat or outrigger, run it into a cliff at full speed. Verify HP decreases by a noticeable amount in one tick.
- [ ] Slowly drift into the same cliff at low speed. Verify HP does NOT decrease.
- [ ] Crash, then immediately reverse and crash again within 1s. Verify the second hit does NOT cause damage (cooldown).

**Storm damage:**

- [ ] Place a boat in deep ocean. Run `/weather setprecip storm` (or `/weather randomwind`) to force high winds. Watch HP over ~30s; it should slowly tick down.
- [ ] Pull the boat onto land. Force the same storm. Verify HP does NOT decrease.
- [ ] Force calm weather (`/weather setprecip clear`). Verify HP does NOT decrease.

**Repair:**

- [ ] Damage a boat to ~50% HP. Hold a wooden bucket of glue in hand, right-click and hold on the boat. After ~1.5s, verify HP restored by ~5, glue drained, repair sound played.
- [ ] Repeat with a bucket of marine varnish. Verify ~12.5 HP restored per use.
- [ ] Try to repair a full-HP boat. Verify a chat message says "This boat is already in perfect condition." and no liquid is drained.
- [ ] Try with a bucket of plain water. Verify no damage is repaired and no liquid is drained.

**Death and wreckage:**

- [ ] Spawn a fresh `boat-outrigger-varnished`, kill it from full HP. Verify wreckage drops at the death position: roughly 40% of `32 planks + 24 supportbeams + 12 rope + 8 sail` (so ~13 planks, ~10 supportbeams, ~5 rope, ~3 sail). Items bob on the surface (slight upward velocity).
- [ ] Spawn a `boat-raft` in shallow water on land, kill it. Wreckage should drop with `dropFloating: true` velocity but settle on land normally.
- [ ] Mount a boat, kill it via repeated arrows from a friend or `/entity hurt`. Verify the rider is dismounted by the vanilla EntityBoat death handler.

**Regression checks:**

- [ ] Place lanterns, baskets, and other accessories on a boat. Kill it. Verify the contents drop (vanilla `dropContentsOnDeath` behavior is preserved).
- [ ] Right-click each boat with a knife to deconstruct it (alive). Verify deconstruction still produces the full materials, not the wreckage fraction.

If all checks pass, the feature is complete.

- [ ] **Final commit** (only if any tweaks were needed during playtest — otherwise skip):

```bash
git add -A
git commit -m "$(cat <<'EOF'
fix(boats): playtest tuning adjustments

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Self-Review Notes (already applied)

- **Spec coverage:** Each spec section (architecture, JSON schema, damage sources, death/wreckage, vanilla patches, item patches) maps to one or more tasks above. Tasks 1–5 cover the boat behavior, Task 6 covers the item behavior, Tasks 7–10 cover JSON wiring, Task 11 is the manual playtest.
- **Type/method names:** `EntityBehaviorShipMechanics` and `BehaviorBoatRepair` are referenced consistently. The behavior code is `shipmechanics` (boat-side) and `boatrepair` (item-side).
- **Placeholders:** none — every step shows the actual code or JSON to write.
- **Storm `requiresDeepWater` semantics:** the implementation samples 3 blocks below the boat's current position; if any is non-liquid, deep-water is false. This matches the spec.
- **Wreckage fractional promotion:** implemented in Task 5 via `Math.Floor(scaled) + (rand < remainder ? 1 : 0)`. Matches the spec exactly.
