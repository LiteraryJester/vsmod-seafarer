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

        // Periodic (storm) work goes here in Task 4 — leave the accumulator wired up.
        tickAccum += deltaTime;
        if (tickAccum < TickIntervalSeconds) return;
        tickAccum = 0f;
    }
}
