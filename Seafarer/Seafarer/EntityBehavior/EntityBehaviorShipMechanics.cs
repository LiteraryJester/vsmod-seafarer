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
