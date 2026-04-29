# Boat Traits Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a JSON-driven trait system for boats that surfaces sail (canvas/oiled/waxed) and material (seasoned/varnished) traits with HP, speed, and storm-damage modifiers, displayed in look-at info with rarity color coding.

**Architecture:** Two new code files (`BoatTrait` + `BehaviorBoatSail`) plus extensions to the existing `EntityBehaviorShipMechanics`. Traits are stored as `code` strings under `entity.WatchedAttributes.shipTraits`; the registry (loaded once from `seafarer:config/boat-traits.json`) supplies live rarity/modifier values so balance changes propagate to existing saves without migration.

**Tech Stack:** C# 10, .NET 10, Vintage Story 1.21+ modding API. Built with `dotnet build`. No automated tests — verification is `dotnet build` plus the manual playtest checklist at the end.

**Spec:** `Seafarer/docs/superpowers/specs/2026-04-28-boat-traits-design.md`

---

## File Structure

**New files:**

- `Seafarer/Seafarer/EntityBehavior/BoatTrait.cs` — data model, rarity enum, registry. Single responsibility: define the trait shape and provide lookup.
- `Seafarer/Seafarer/CollectibleBehavior/BehaviorBoatSail.cs` — item-side right-click handler that applies a sail trait to a targeted boat.
- `Seafarer/Seafarer/assets/seafarer/config/boat-traits.json` — the trait registry data.
- `Seafarer/Seafarer/assets/seafarer/patches/canvas-sail-boatsail.json` — adds the `boatsail` behavior to the canvas-sail item.
- `Seafarer/Seafarer/assets/seafarer/patches/oiled-canvas-sail-boatsail.json` — same for oiled.
- `Seafarer/Seafarer/assets/seafarer/patches/waxed-canvas-sail-boatsail.json` — same for waxed.

**Modified files:**

- `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs` — adds trait storage helpers, `RecomputeTraitEffects`, material-trait resolution at spawn, storm-damage scale, and an extension to `GetInfoText`.
- `Seafarer/Seafarer/SeafarerModSystem.cs` — registers `boatsail` collectible behavior and loads `BoatTraitRegistry` in `AssetsFinalize`.
- `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-outrigger.json` — adds `hasSailSlot` and `materialTraits` per variant.
- `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-logbarge.json` — adds `hasSailSlot: true`.
- `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-raft-shipmechanics.json` — adds `hasSailSlot: false`.
- `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-sailed-shipmechanics.json` — adds `hasSailSlot: true`.
- `Seafarer/Seafarer/assets/seafarer/lang/en.json` — trait names, rarity names, colored line templates, two new chat strings.

---

## Working Directory

All commands assume `/mnt/d/Development/vs/vsmod-seafarer` as the working directory unless otherwise stated.

The build environment variable `VINTAGE_STORY` must be set:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
```

---

## Task 1: Create `BoatTrait` Data Model + Registry

Goal: a self-contained file with the trait record, rarity enum (with hex color helper), and a static registry stub. The registry's load method is a no-op for now; Task 2 wires the asset load.

**Files:**
- Create: `Seafarer/Seafarer/EntityBehavior/BoatTrait.cs`

- [ ] **Step 1: Write the file**

```csharp
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Seafarer;

public enum TraitRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public static class TraitRarityExtensions
{
    public static string LangKey(this TraitRarity r) => r switch
    {
        TraitRarity.Common => "common",
        TraitRarity.Uncommon => "uncommon",
        TraitRarity.Rare => "rare",
        TraitRarity.Epic => "epic",
        TraitRarity.Legendary => "legendary",
        _ => "common"
    };
}

public class BoatTrait
{
    public string Code { get; set; } = "";
    public string Source { get; set; } = "";
    public TraitRarity Rarity { get; set; } = TraitRarity.Common;
    public float SpeedBonus { get; set; }
    public float HealthBonus { get; set; }
    public float StormDamageScale { get; set; } = 1f;
    public string? DropItem { get; set; }
}

public static class BoatTraitRegistry
{
    private static readonly Dictionary<string, BoatTrait> traits = new();

    public static BoatTrait? Get(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        return traits.TryGetValue(code, out var t) ? t : null;
    }

    public static void Load(ICoreAPI api)
    {
        traits.Clear();

        var asset = api.Assets.TryGet(new AssetLocation("seafarer:config/boat-traits.json"));
        if (asset == null)
        {
            api.Logger.Warning("[seafarer] boat-traits.json missing; trait system disabled.");
            return;
        }

        JsonObject root;
        try
        {
            root = new JsonObject(Newtonsoft.Json.Linq.JToken.Parse(asset.ToText()));
        }
        catch (System.Exception e)
        {
            api.Logger.Warning("[seafarer] boat-traits.json parse failed: {0}", e.Message);
            return;
        }

        var traitsNode = root["traits"];
        if (!traitsNode.Exists || traitsNode.Token is not Newtonsoft.Json.Linq.JObject obj) return;

        foreach (var prop in obj.Properties())
        {
            var jt = new JsonObject(prop.Value);
            var rarityStr = jt["rarity"].AsString("common").ToLowerInvariant();
            var rarity = rarityStr switch
            {
                "uncommon" => TraitRarity.Uncommon,
                "rare" => TraitRarity.Rare,
                "epic" => TraitRarity.Epic,
                "legendary" => TraitRarity.Legendary,
                _ => TraitRarity.Common
            };

            traits[prop.Name] = new BoatTrait
            {
                Code = prop.Name,
                Source = jt["source"].AsString("sail"),
                Rarity = rarity,
                SpeedBonus = jt["speedBonus"].AsFloat(0f),
                HealthBonus = jt["healthBonus"].AsFloat(0f),
                StormDamageScale = jt["stormDamageScale"].AsFloat(1f),
                DropItem = jt["dropItem"].Exists ? jt["dropItem"].AsString() : null
            };
        }

        api.Logger.Notification("[seafarer] loaded {0} boat traits.", traits.Count);
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/BoatTrait.cs
git commit -m "$(cat <<'EOF'
feat(boats): add BoatTrait data model and registry

Defines the trait record (code, source, rarity, modifiers,
optional drop item) and a static registry that loads from
seafarer:config/boat-traits.json. Subsequent tasks call
Load() at AssetsFinalize and consume traits from the
EntityBehaviorShipMechanics path.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Create the Registry JSON + Wire Load on AssetsFinalize

Goal: write the trait JSON and call `BoatTraitRegistry.Load` from the mod's `AssetsFinalize` so it's populated before any entity initializes.

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/config/boat-traits.json`
- Modify: `Seafarer/Seafarer/SeafarerModSystem.cs`

- [ ] **Step 1: Write the trait JSON**

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

- [ ] **Step 2: Wire the load call**

In `Seafarer/Seafarer/SeafarerModSystem.cs`, locate the `AssetsFinalize` method (currently around line 75 — it begins with `public override void AssetsFinalize(ICoreAPI api)`). The body currently looks like:

```csharp
        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);
            if (api is ICoreServerAPI sapi)
            {
                LoadConfigs(sapi, log: true);
                if (sapi.ModLoader.IsModEnabled("configlib"))
                {
                    HookConfigLib(sapi);
                }
            }
        }
```

Replace it with:

```csharp
        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);
            BoatTraitRegistry.Load(api);
            if (api is ICoreServerAPI sapi)
            {
                LoadConfigs(sapi, log: true);
                if (sapi.ModLoader.IsModEnabled("configlib"))
                {
                    HookConfigLib(sapi);
                }
            }
        }
```

The registry load runs on both client and server (it must, since look-at info renders client-side).

- [ ] **Step 3: Build to verify**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/config/boat-traits.json Seafarer/Seafarer/SeafarerModSystem.cs
git commit -m "$(cat <<'EOF'
feat(boats): trait registry data + load at AssetsFinalize

Five traits: Canvas/Oiled/Waxed Sail (sail source) and
Seasoned/Varnished Hull (material source). Registry loads
on both sides so client-side look-at info can read trait
metadata.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Trait Storage + `RecomputeTraitEffects` in `EntityBehaviorShipMechanics`

Goal: introduce trait read/write helpers on the entity behavior, plus a single `RecomputeTraitEffects` that reapplies HP, speed, and storm-damage modifiers from the persisted traits. No callers yet — Task 4 will wire material trait, Task 5 will integrate the storm scale, and Task 7 will call from the item side.

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs`

- [ ] **Step 1: Add a cached storm scale field and a `HasSailSlot` getter**

In `EntityBehaviorShipMechanics.cs`, find the existing wreckage-config fields (around line 33-35):

```csharp
    private float wreckageDropFraction = 0.4f;
    private bool wreckageDropFloating = true;
```

Append immediately after:

```csharp
    private float stormDamageMultiplier = 1f;

    public bool HasSailSlot => cfg?["hasSailSlot"].AsBool(false) ?? false;
```

- [ ] **Step 2: Add the trait helpers near the end of the class**

In the same file, find the closing `}` of the class (currently around line 272 — the LAST `}` in the file). Immediately BEFORE that closing brace, append the following methods. Be sure these are inside the class, not after it.

```csharp
    private ITreeAttribute GetOrCreateTraitTree()
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("shipTraits");
        if (tree == null)
        {
            tree = new TreeAttribute();
            entity.WatchedAttributes.SetAttribute("shipTraits", tree);
        }
        return tree;
    }

    private string? GetTraitCode(string source)
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("shipTraits");
        var sub = tree?.GetTreeAttribute(source);
        return sub?.GetString("code");
    }

    public void ApplyTrait(string source, string code)
    {
        var tree = GetOrCreateTraitTree();
        var sub = new TreeAttribute();
        sub.SetString("code", code);
        tree.SetAttribute(source, sub);
        entity.WatchedAttributes.MarkPathDirty("shipTraits");
    }

    public string? RemoveTrait(string source)
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("shipTraits");
        if (tree == null) return null;
        var sub = tree.GetTreeAttribute(source);
        if (sub == null) return null;
        var code = sub.GetString("code");
        tree.RemoveAttribute(source);
        entity.WatchedAttributes.MarkPathDirty("shipTraits");
        return code;
    }

    public void RecomputeTraitEffects()
    {
        var traits = ActiveTraits();

        var healthBh = entity.GetBehavior<EntityBehaviorHealth>();
        if (healthBh != null)
        {
            float baseHp = cfg?["health"].AsFloat(healthBh.BaseMaxHealth) ?? healthBh.BaseMaxHealth;
            float bonus = 0f;
            foreach (var t in traits) bonus += t.HealthBonus;
            float oldMax = healthBh.MaxHealth;
            healthBh.BaseMaxHealth = baseHp + bonus;
            healthBh.UpdateMaxHealth();
            // Clamp current HP to the new max if we lost capacity (e.g., sail downgrade).
            if (healthBh.Health > healthBh.MaxHealth) healthBh.Health = healthBh.MaxHealth;
            // If MaxHP grew, keep current HP unchanged (player feels the buffer); the apply
            // path bumps current Health by the delta separately (see ApplyAndCreditDelta).
        }

        float speedBonus = 0f;
        foreach (var t in traits) speedBonus += t.SpeedBonus;
        if (speedBonus > 0f)
        {
            entity.Stats.Set("walkspeed", "shipTraits", 1f + speedBonus, persistent: true);
        }
        else
        {
            entity.Stats.Remove("walkspeed", "shipTraits");
        }

        float scale = 1f;
        foreach (var t in traits) scale *= t.StormDamageScale;
        stormDamageMultiplier = scale;
    }

    private System.Collections.Generic.List<BoatTrait> ActiveTraits()
    {
        var list = new System.Collections.Generic.List<BoatTrait>();
        foreach (var source in new[] { "material", "sail" })
        {
            var code = GetTraitCode(source);
            if (code == null) continue;
            var t = BoatTraitRegistry.Get(code);
            if (t != null) list.Add(t);
        }
        return list;
    }

    public void ApplyAndCreditDelta(string source, string newCode)
    {
        var healthBh = entity.GetBehavior<EntityBehaviorHealth>();
        float oldMax = healthBh?.MaxHealth ?? 0f;
        ApplyTrait(source, newCode);
        RecomputeTraitEffects();
        if (healthBh != null && healthBh.MaxHealth > oldMax)
        {
            healthBh.Health = System.Math.Min(healthBh.Health + (healthBh.MaxHealth - oldMax), healthBh.MaxHealth);
        }
    }
```

- [ ] **Step 3: Build to verify**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs
git commit -m "$(cat <<'EOF'
feat(boats): trait storage helpers and RecomputeTraitEffects

Persists active traits as code strings under
WatchedAttributes.shipTraits. RecomputeTraitEffects rebuilds
BaseMaxHealth, the walkspeed stat modifier, and the cached
storm-damage scale from whatever traits are currently active.
ApplyAndCreditDelta is the apply path that also bumps current
Health by any new max-HP delta. Storage and recompute are
plumbed but no caller invokes them yet — that lands in the
following tasks.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Apply Material Trait at First Spawn + Recompute on Load

Goal: in `AfterInitialized`, resolve the material trait from the entity variant once on first spawn, and always call `RecomputeTraitEffects` so the speed stat modifier is reapplied on every load (stat modifiers don't survive serialization, only trait codes do).

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs`

- [ ] **Step 1: Extend `AfterInitialized`**

In `EntityBehaviorShipMechanics.cs`, find the existing `AfterInitialized` method (currently around line 70-95). It currently ends with this block:

```csharp
        healthBh.BaseMaxHealth = configuredHealth;
        healthBh.UpdateMaxHealth();
        if (onFirstSpawn)
        {
            healthBh.Health = healthBh.MaxHealth;
        }

        // Boats don't heal themselves — only player repair.
        entity.WatchedAttributes.SetFloat("regenSpeed", 0f);
    }
```

Replace that whole tail with:

```csharp
        healthBh.BaseMaxHealth = configuredHealth;
        healthBh.UpdateMaxHealth();
        if (onFirstSpawn)
        {
            healthBh.Health = healthBh.MaxHealth;
        }

        // Boats don't heal themselves — only player repair.
        entity.WatchedAttributes.SetFloat("regenSpeed", 0f);

        if (onFirstSpawn) ResolveMaterialTrait();
        RecomputeTraitEffects();
    }
```

- [ ] **Step 2: Add `ResolveMaterialTrait` near the other trait helpers**

Append the following method inside the class, just before the closing brace (next to the helpers added in Task 3):

```csharp
    private void ResolveMaterialTrait()
    {
        if (cfg == null || !cfg.KeyExists("materialTraits")) return;
        var map = cfg["materialTraits"].Token as Newtonsoft.Json.Linq.JObject;
        if (map == null) return;

        string code = entity.Code.ToString();
        foreach (var prop in map.Properties())
        {
            // The key is a material identifier (e.g., "seasoned", "varnished").
            // Match if the entity code contains it as a hyphen-delimited segment.
            string segment = "-" + prop.Name;
            if (code.EndsWith(segment) || code.Contains(segment + "-"))
            {
                ApplyTrait("material", prop.Value.ToString());
                return;
            }
        }
    }
```

- [ ] **Step 3: Build to verify**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs
git commit -m "$(cat <<'EOF'
feat(boats): resolve material trait at spawn + recompute on load

ResolveMaterialTrait inspects extendShipMechanics.materialTraits
and matches keys against the entity variant code (boat-outrigger-
varnished etc.). On first spawn the trait code is persisted under
shipTraits/material; on reload we skip resolution but still call
RecomputeTraitEffects so the walkspeed stat modifier is reapplied
(only the trait code persists, not the stat modifier itself).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Wire Storm Damage Through the Trait Multiplier

Goal: storm damage applied in `ApplyStormDamage` should be scaled by `stormDamageMultiplier` so traits like Varnished Hull reduce environmental wear.

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs`

- [ ] **Step 1: Apply the multiplier**

In `ApplyStormDamage` (currently around line 128-158), find the line that computes the damage value:

```csharp
        float damage = stormDamagePerSecond * intervalSeconds;
```

Replace with:

```csharp
        float damage = stormDamagePerSecond * intervalSeconds * stormDamageMultiplier;
        if (damage <= 0f) return;
```

The early return guards the pathological case of a hypothetical trait multiplier being 0 (no work to do).

- [ ] **Step 2: Build to verify**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs
git commit -m "$(cat <<'EOF'
feat(boats): scale storm damage by trait multiplier

ApplyStormDamage now multiplies the per-tick damage by the
cached stormDamageMultiplier (product over active traits'
StormDamageScale). Varnished Hull reduces storm damage to
75% per tick; combat and collision are unchanged.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Render Trait Lines in `GetInfoText`

Goal: extend the existing hull-bar `GetInfoText` to append one colored line per active trait. Lang templates carry the color markup so C# never hardcodes hex.

**Files:**
- Modify: `Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs`

- [ ] **Step 1: Extend the override**

In `EntityBehaviorShipMechanics.cs`, find the existing `GetInfoText` method (currently around line 173). Its body currently ends with the `infotext.AppendLine(Lang.Get("seafarer:boat-hull", ...));` call. Append the following AFTER that line, still inside the method body:

```csharp
        var traits = ActiveTraits();
        foreach (var t in traits)
        {
            string name = Lang.Get("seafarer:trait-" + t.Code);
            string rarity = Lang.Get("seafarer:rarity-" + t.Rarity.LangKey());
            infotext.AppendLine(Lang.Get("seafarer:trait-line-" + t.Rarity.LangKey(), name, rarity));
        }
```

The order is `material` first, then `sail` (preserved by `ActiveTraits`'s loop order). If a trait code is unknown to the registry, `ActiveTraits` already skips it.

- [ ] **Step 2: Build to verify**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/EntityBehavior/EntityBehaviorShipMechanics.cs
git commit -m "$(cat <<'EOF'
feat(boats): show traits in look-at info with rarity colors

Iterates active traits (material then sail) and emits a colored
line per trait via the seafarer:trait-line-{rarity} lang key.
Color hex lives in the lang file, not C#.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Create `BehaviorBoatSail` (Item-Side) + Register It

Goal: a `CollectibleBehavior` that on right-click-target-entity applies a sail trait. Drops the previous sail's cloth at the boat if the previous trait declared a `dropItem`.

**Files:**
- Create: `Seafarer/Seafarer/CollectibleBehavior/BehaviorBoatSail.cs`
- Modify: `Seafarer/Seafarer/SeafarerModSystem.cs`

- [ ] **Step 1: Create the behavior file**

Write `Seafarer/Seafarer/CollectibleBehavior/BehaviorBoatSail.cs`:

```csharp
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Seafarer;

public class BehaviorBoatSail : CollectibleBehavior
{
    public const string Code = "boatsail";

    private string traitCode = "";

    public BehaviorBoatSail(CollectibleObject collObj) : base(collObj) { }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        traitCode = properties["trait"].AsString("");
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling,
        ref EnumHandling handling)
    {
        if (entitySel?.Entity == null) return;

        var ship = entitySel.Entity.GetBehavior<EntityBehaviorShipMechanics>();
        if (ship == null) return;

        if (!ship.HasSailSlot)
        {
            if (byEntity.World.Side == EnumAppSide.Client && byEntity is EntityPlayer ep1)
            {
                (ep1.Player as IClientPlayer)?.ShowChatNotification(Lang.Get("seafarer:boat-no-sail-slot"));
            }
            handling = EnumHandling.PreventSubsequent;
            handHandling = EnumHandHandling.PreventDefault;
            return;
        }

        if (string.IsNullOrEmpty(traitCode)) return;
        var trait = BoatTraitRegistry.Get(traitCode);
        if (trait == null) return;

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;

        if (byEntity.World.Side != EnumAppSide.Server) return;

        // Drop the previous sail's cloth if any.
        var oldCode = ship.RemoveTrait("sail");
        if (oldCode != null)
        {
            var oldTrait = BoatTraitRegistry.Get(oldCode);
            if (oldTrait?.DropItem != null)
            {
                var item = byEntity.World.GetItem(new AssetLocation(oldTrait.DropItem));
                if (item != null)
                {
                    byEntity.World.SpawnItemEntity(
                        new ItemStack(item, 1),
                        entitySel.Entity.ServerPos.XYZ.AddCopy(0, 0.2, 0));
                }
            }
        }

        ship.ApplyAndCreditDelta("sail", trait.Code);
        slot.TakeOut(1);
        slot.MarkDirty();

        byEntity.World.PlaySoundAt(
            new AssetLocation("game:sounds/block/cloth"),
            byEntity, byEntity is EntityPlayer ep2 ? ep2.Player : null);

        if (byEntity is EntityPlayer ep3)
        {
            (ep3.Player as IServerPlayer)?.SendMessage(
                GlobalConstants.GeneralChatGroup,
                Lang.Get("seafarer:sail-applied"),
                EnumChatType.Notification);
        }
    }
}
```

- [ ] **Step 2: Register the class in `SeafarerModSystem.cs`**

In `Seafarer/Seafarer/SeafarerModSystem.cs`, find the existing line that registers `boatrepair`:

```csharp
            api.RegisterCollectibleBehaviorClass("boatrepair", typeof(BehaviorBoatRepair));
```

Add immediately after it:

```csharp
            api.RegisterCollectibleBehaviorClass("boatsail", typeof(BehaviorBoatSail));
```

- [ ] **Step 3: Build to verify**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/CollectibleBehavior/BehaviorBoatSail.cs Seafarer/Seafarer/SeafarerModSystem.cs
git commit -m "$(cat <<'EOF'
feat(boats): item-side BehaviorBoatSail for sail trait apply

Registered as 'boatsail'. Reads its target trait from JSON
properties. On right-click against an entity with shipmechanics:
checks HasSailSlot, drops the prior sail's cloth (if any) at the
boat, applies the new trait, recomputes effects, consumes one,
plays the cloth sound, and sends a confirmation chat.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Wire `hasSailSlot` and `materialTraits` Onto Boat Configs

Goal: every boat declares whether sails apply and which material maps to which trait. Vanilla raft refuses sails; outrigger/logbarge/sailed accept them; outrigger maps its two materials.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-outrigger.json`
- Modify: `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-logbarge.json`
- Modify: `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-raft-shipmechanics.json`
- Modify: `Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-sailed-shipmechanics.json`

- [ ] **Step 1: Edit `boat-outrigger.json`**

In the file, locate the `extendShipMechanicsByType` block (currently has two keys, `*-varnished` and `*-seasoned`). Each variant currently has `health`, `collision`, `storm`, `wreckage`. To both inner objects, add `hasSailSlot: true` and `materialTraits` AFTER `health` and BEFORE `collision`. The varnished entry should look like:

```json
      "*-varnished": {
        "health": 280,
        "hasSailSlot": true,
        "materialTraits": { "varnished": "varnished-hull" },
        "collision": { "minSpeed": 0.30, "damagePerSpeedUnit": 8.0, "cooldownSeconds": 1.0 },
        "storm": { "minWindSpeed": 0.65, "damagePerSecond": 0.4, "requiresDeepWater": true },
        "wreckage": { "dropFraction": 0.4, "dropFloating": true }
      },
```

The seasoned entry similarly:

```json
      "*-seasoned": {
        "health": 200,
        "hasSailSlot": true,
        "materialTraits": { "seasoned": "seasoned-hull" },
        "collision": { "minSpeed": 0.30, "damagePerSpeedUnit": 8.0, "cooldownSeconds": 1.0 },
        "storm": { "minWindSpeed": 0.65, "damagePerSecond": 0.4, "requiresDeepWater": true },
        "wreckage": { "dropFraction": 0.4, "dropFloating": true }
      }
```

- [ ] **Step 2: Edit `boat-logbarge.json`**

Locate the `extendShipMechanics` block (NOT `*ByType`). The current block has health=100 plus collision/storm/wreckage. Add `"hasSailSlot": true` after `health`. The block becomes:

```json
    "extendShipMechanics": {
      "health": 100,
      "hasSailSlot": true,
      "collision": { "minSpeed": 0.30, "damagePerSpeedUnit": 8.0, "cooldownSeconds": 1.0 },
      "storm": { "minWindSpeed": 0.65, "damagePerSecond": 0.4, "requiresDeepWater": true },
      "wreckage": { "dropFraction": 0.5, "dropFloating": true }
    }
```

(Logbarge has no material traits — only one variant.)

- [ ] **Step 3: Edit `vanilla-boat-raft-shipmechanics.json`**

The patch file's last operation is a single `add` on `/attributes/extendShipMechanics`. Update the `value` object to include `hasSailSlot: false`. The relevant operation becomes:

```json
  {
    "op": "add",
    "path": "/attributes/extendShipMechanics",
    "value": {
      "health": 60,
      "hasSailSlot": false,
      "collision": { "minSpeed": 0.30, "damagePerSpeedUnit": 8.0, "cooldownSeconds": 1.0 },
      "storm": { "minWindSpeed": 0.65, "damagePerSecond": 0.4, "requiresDeepWater": true },
      "wreckage": { "dropFraction": 0.5, "dropFloating": true }
    },
    "file": "game:entities/nonliving/boat-raft"
  }
```

- [ ] **Step 4: Edit `vanilla-boat-sailed-shipmechanics.json`**

Same shape, sailed boats accept sails. Update the `value` object:

```json
  {
    "op": "add",
    "path": "/attributes/extendShipMechanics",
    "value": {
      "health": 120,
      "hasSailSlot": true,
      "collision": { "minSpeed": 0.30, "damagePerSpeedUnit": 8.0, "cooldownSeconds": 1.0 },
      "storm": { "minWindSpeed": 0.65, "damagePerSecond": 0.4, "requiresDeepWater": true },
      "wreckage": { "dropFraction": 0.4, "dropFloating": true }
    },
    "file": "game:entities/nonliving/boat-sailed"
  }
```

- [ ] **Step 5: Build to verify**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-outrigger.json Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-logbarge.json Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-raft-shipmechanics.json Seafarer/Seafarer/assets/seafarer/patches/vanilla-boat-sailed-shipmechanics.json
git commit -m "$(cat <<'EOF'
feat(boats): wire hasSailSlot + materialTraits per boat

Outrigger maps seasoned/varnished variants to their hull traits
and accepts sails. Logbarge accepts sails (no material traits).
Vanilla raft refuses sails (paddled raft, not a sailing boat).
Vanilla sailed accepts sails (no material traits).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Patch Sail Items With `boatsail` Behavior

Goal: each cloth-sail resource item declares which trait it applies.

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/patches/canvas-sail-boatsail.json`
- Create: `Seafarer/Seafarer/assets/seafarer/patches/oiled-canvas-sail-boatsail.json`
- Create: `Seafarer/Seafarer/assets/seafarer/patches/waxed-canvas-sail-boatsail.json`

- [ ] **Step 1: Write the canvas patch**

```json
[
  {
    "op": "addmerge",
    "path": "/behaviors",
    "value": [
      { "name": "boatsail", "trait": "canvas-sail" }
    ],
    "file": "seafarer:itemtypes/resource/canvas-sail.json"
  }
]
```

- [ ] **Step 2: Write the oiled-canvas patch**

```json
[
  {
    "op": "addmerge",
    "path": "/behaviors",
    "value": [
      { "name": "boatsail", "trait": "oiled-sail" }
    ],
    "file": "seafarer:itemtypes/resource/oiled-canvas-sail.json"
  }
]
```

- [ ] **Step 3: Write the waxed-canvas patch**

```json
[
  {
    "op": "addmerge",
    "path": "/behaviors",
    "value": [
      { "name": "boatsail", "trait": "waxed-sail" }
    ],
    "file": "seafarer:itemtypes/resource/waxed-canvas-sail.json"
  }
]
```

- [ ] **Step 4: Run the asset validator**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer && python3 vs_validators/validate-assets.py 2>&1 | tail -10
```

Expected: 0 new errors.

- [ ] **Step 5: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/patches/canvas-sail-boatsail.json Seafarer/Seafarer/assets/seafarer/patches/oiled-canvas-sail-boatsail.json Seafarer/Seafarer/assets/seafarer/patches/waxed-canvas-sail-boatsail.json
git commit -m "$(cat <<'EOF'
feat(boats): patch sail resources with boatsail behavior

Each cloth-sail item (canvas, oiled-canvas, waxed-canvas) gets
the boatsail CollectibleBehavior with its target trait code.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Lang Strings

Goal: trait names, rarity names, the five colored line templates, and the two new chat strings.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/lang/en.json`

- [ ] **Step 1: Add the entries**

In `en.json`, the file currently ends with:

```json
	"seafarer:boatrepair-fullhp": "This boat is already in perfect condition.",
	"seafarer:boat-hull": "Hull: {0}/{1} {2}"
}
```

Replace those final two entries plus the closing `}` with:

```json
	"seafarer:boatrepair-fullhp": "This boat is already in perfect condition.",
	"seafarer:boat-hull": "Hull: {0}/{1} {2}",
	"seafarer:boat-no-sail-slot": "This boat can't use a sail.",
	"seafarer:sail-applied": "New sail rigged.",

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

	"seafarer:trait-line-common": "<font color=\"#9D9D9D\">■ {0} ({1})</font>",
	"seafarer:trait-line-uncommon": "<font color=\"#1EFF00\">■ {0} ({1})</font>",
	"seafarer:trait-line-rare": "<font color=\"#0070FF\">■ {0} ({1})</font>",
	"seafarer:trait-line-epic": "<font color=\"#A335EE\">■ {0} ({1})</font>",
	"seafarer:trait-line-legendary": "<font color=\"#FF8000\">■ {0} ({1})</font>"
}
```

- [ ] **Step 2: Run the asset validator**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer && python3 vs_validators/validate-assets.py 2>&1 | tail -10
```

Expected: 0 new errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/lang/en.json
git commit -m "$(cat <<'EOF'
feat(boats): lang strings for traits + chat messages

Adds five trait names, five rarity names, five colored
line templates (one per rarity tier), and the two new
chat messages used by the sail-apply flow.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 11: Manual Playtest

Goal: confirm the system works end-to-end before declaring complete. No automated tests in this codebase.

**Build and launch:**

- [ ] Build:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

- [ ] Launch the game with the mod and load a creative test world over deep ocean.

**Material traits:**

- [ ] `/entity spawn boat-outrigger-seasoned` — look at it. Verify the look-at info shows:
   - `Hull: 250/250 ████████████████` (200 base + 50 from seasoned)
   - A green `■ Seasoned Hull (Uncommon)` line.
- [ ] `/entity spawn boat-outrigger-varnished` — look at it. Verify:
   - `Hull: 355/355` (280 base + 75 from varnished)
   - A blue `■ Varnished Hull (Rare)` line.
- [ ] `/entity spawn boat-logbarge` — look at it. No trait line shown (no material trait).
- [ ] `/entity spawn boat-raft` — same, no trait line.

**Sail apply (positive path):**

- [ ] Holding a `seafarer:canvas-sail` resource, right-click the logbarge. Verify:
   - Cloth sound plays.
   - Chat shows "New sail rigged."
   - One canvas-sail consumed from the stack.
   - Look-at now shows a gray `■ Canvas Sail (Common)` line.
   - Hull max is 110/110 (100 base + 10 trait).
- [ ] Mount the boat and paddle. Verify it's noticeably faster than the same boat without a sail (5% speed bump).

**Sail apply (replacement):**

- [ ] On the same logbarge that now has a canvas sail, right-click with a `seafarer:waxed-canvas-sail` (waxed). Verify:
   - One waxed consumed.
   - One canvas-sail dropped at the boat (pick it up — should be the same item you originally placed).
   - Look-at line replaces with a blue `■ Waxed Sail (Rare)`.
   - Hull max is 130/130 (100 base + 30 trait).

**Sail apply (refused):**

- [ ] Right-click any sail item on a vanilla `boat-raft`. Verify:
   - No consume.
   - Chat shows "This boat can't use a sail."
   - No trait line on the boat after.

**Stacked traits:**

- [ ] Apply a waxed-sail to a `boat-outrigger-varnished`. Verify look-at shows BOTH lines:
   - Blue `■ Varnished Hull (Rare)`
   - Blue `■ Waxed Sail (Rare)`
   - Hull max is 385/385 (280 + 75 + 30).

**Storm-damage scaling:**

- [ ] Place a varnished outrigger and a vanilla sailed boat in deep water during a storm (`/weather setprecip storm`). Watch HP for ~30 seconds. The varnished boat should lose noticeably less HP than the vanilla sailed (75% rate). Combat damage (sword hits) should be unchanged.

**Persistence:**

- [ ] Save and quit. Reload. Verify all trait lines and HP totals are unchanged. Verify the speed bonus still applies (paddle the boat).

**Repair interaction:**

- [ ] Damage the trait-equipped logbarge to ~50% HP. Repair with glue. Verify HP recovers correctly (does not exceed the trait-modified MaxHP).

**Death:**

- [ ] Kill the trait-equipped logbarge from full HP. Verify wreckage drops as expected (per the existing health-system rules); the sail is NOT respawned separately.

If all checks pass, the feature is complete.

- [ ] **Final commit** (only if any tweaks were needed during playtest — otherwise skip):

```bash
git add -A
git commit -m "$(cat <<'EOF'
fix(boats): playtest tuning for trait system

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Self-Review Notes (already applied)

- **Spec coverage:** Task 1-2 = registry. Task 3 = storage + recompute. Task 4 = material at spawn. Task 5 = storm scale. Task 6 = look-at display. Task 7 = sail apply. Task 8 = boat configs. Task 9 = item patches. Task 10 = lang. Task 11 = playtest. Every section of the spec has a task.
- **Type/method names:** `BoatTrait`, `TraitRarity`, `BoatTraitRegistry`, `BehaviorBoatSail`, `EntityBehaviorShipMechanics.HasSailSlot`, `ApplyTrait/RemoveTrait/ApplyAndCreditDelta/RecomputeTraitEffects/ResolveMaterialTrait/ActiveTraits` are referenced consistently across tasks.
- **No placeholders:** every step shows actual code or JSON.
- **API verifications:** `EntityStats.Set(category, code, value, persistent)` confirmed via API decompilation. `JObject.Properties()` is standard Newtonsoft. `Lang.Get` formatting with `{0}` placeholders is standard VS pattern.
- **Trait-tree variant matching** in Task 4 uses substring/segment match, which handles both the canonical form `boat-outrigger-varnished` and any extended variants without false positives.
