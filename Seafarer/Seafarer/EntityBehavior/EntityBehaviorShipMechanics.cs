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

        float damage = stormDamagePerSecond * intervalSeconds;
        entity.ReceiveDamage(
            new DamageSource { Source = EnumDamageSource.Weather, Type = EnumDamageType.Gravity },
            damage);
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
}
