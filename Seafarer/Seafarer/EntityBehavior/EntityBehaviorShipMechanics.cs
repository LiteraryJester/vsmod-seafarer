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
}
