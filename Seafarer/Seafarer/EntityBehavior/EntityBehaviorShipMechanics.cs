using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
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

    private const float TickIntervalSeconds = 1.0f;

    private float tickAccum;
    private float collisionCooldown;
    private double prevSpeed;

    private float collisionMinSpeed = 0.30f;
    private float collisionDamagePerSpeedUnit = 8.0f;
    private float collisionCooldownSeconds = 1.0f;

    private float stormMinWindSpeed = 0.65f;
    private float stormDamagePerSecond = 0.4f;
    private bool stormRequiresDeepWater = true;

    private float wreckageDropFraction = 0.4f;
    private bool wreckageDropFloating = true;

    private float stormDamageMultiplier = 1f;

    public bool HasSailSlot => cfg?["hasSailSlot"].AsBool(false) ?? false;

    public EntityBehaviorShipMechanics(Entity entity) : base(entity) { }

    public override string PropertyName() => Code;

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

        if (cfg != null && cfg.KeyExists("storm"))
        {
            var s = cfg["storm"];
            stormMinWindSpeed = s["minWindSpeed"].AsFloat(stormMinWindSpeed);
            stormDamagePerSecond = s["damagePerSecond"].AsFloat(stormDamagePerSecond);
            stormRequiresDeepWater = s["requiresDeepWater"].AsBool(stormRequiresDeepWater);
        }

        if (cfg != null && cfg.KeyExists("wreckage"))
        {
            var w = cfg["wreckage"];
            wreckageDropFraction = w["dropFraction"].AsFloat(wreckageDropFraction);
            wreckageDropFloating = w["dropFloating"].AsBool(wreckageDropFloating);
        }
    }

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

        // Boats don't heal themselves — only player repair.
        entity.WatchedAttributes.SetFloat("regenSpeed", 0f);

        if (onFirstSpawn) ResolveMaterialTrait();
        RecomputeTraitEffects();
    }

    public override void OnGameTick(float deltaTime)
    {
        if (api.Side != EnumAppSide.Server) return;

        if (collisionCooldown > 0f) collisionCooldown -= deltaTime;

        // Sample current horizontal speed every frame so we can detect
        // the impact tick. Storing prevSpeed lets us deal damage based on
        // the speed *before* the wall stopped us.
        var v = entity.Pos.Motion;
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

        tickAccum += deltaTime;
        if (tickAccum < TickIntervalSeconds) return;
        float stormDelta = tickAccum;
        tickAccum = 0f;

        ApplyStormDamage(stormDelta);
    }

    private void ApplyStormDamage(float intervalSeconds)
    {
        if (stormDamagePerSecond <= 0f || stormMinWindSpeed > 1f) return;

        var pos = entity.Pos.AsBlockPos;
        var wind = api.World.BlockAccessor.GetWindSpeedAt(pos);
        double windMag = Math.Sqrt(wind.X * wind.X + wind.Y * wind.Y + wind.Z * wind.Z);
        if (windMag < stormMinWindSpeed) return;

        if (stormRequiresDeepWater && !IsOverDeepWater(pos)) return;

        var healthBh = entity.GetBehavior<EntityBehaviorHealth>();
        if (healthBh == null) return;

        // Bypass ReceiveDamage so the boat doesn't hurt-flash every tick;
        // storm wear-and-tear is environmental, not a "hit".
        float damage = stormDamagePerSecond * intervalSeconds;
        healthBh.Health -= damage;
        entity.WatchedAttributes.MarkPathDirty("health");

        if (healthBh.Health <= 0f)
        {
            entity.Die(EnumDespawnReason.Death,
                new DamageSource { Source = EnumDamageSource.Weather, Type = EnumDamageType.Gravity });
        }
    }

    private bool IsOverDeepWater(BlockPos boatPos)
    {
        // Probe the three blocks beneath the boat. All must be liquid for "deep water".
        var probe = boatPos.DownCopy();
        for (int i = 0; i < 3; i++)
        {
            var block = api.World.BlockAccessor.GetBlock(probe);
            if (block.LiquidCode == null) return false;
            probe.Y--;
        }
        return true;
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        var healthBh = entity.GetBehavior<EntityBehaviorHealth>();
        if (healthBh == null) return;

        float health = Math.Max(healthBh.Health, 0f);
        float maxHealth = healthBh.MaxHealth;
        if (maxHealth <= 0f) return;

        const int barWidth = 16;
        int filled = (int)Math.Round(barWidth * health / maxHealth);
        filled = GameMath.Clamp(filled, 0, barWidth);

        infotext.AppendLine(Lang.Get("seafarer:boat-hull",
            (int)Math.Ceiling(health), (int)maxHealth,
            new string('█', filled) + new string('░', barWidth - filled)));
    }

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
        var spawnPos = entity.Pos.XYZ.AddCopy(0, 0.1, 0);

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

            Vec3d? velocity = null;
            if (wreckageDropFloating)
            {
                velocity = new Vec3d(
                    (rand.NextDouble() - 0.5) * 0.1,
                    0.1,
                    (rand.NextDouble() - 0.5) * 0.1);
            }

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

    private TreeAttribute GetOrCreateTraitTree()
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("shipTraits") as TreeAttribute;
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

    private List<BoatTrait> ActiveTraits()
    {
        var list = new List<BoatTrait>();
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
            healthBh.Health = Math.Min(healthBh.Health + (healthBh.MaxHealth - oldMax), healthBh.MaxHealth);
        }
    }

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
}
