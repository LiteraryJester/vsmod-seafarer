using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

// Subclasses base game ItemRoller. Overrides OnHeldInteractStart to spawn
// the outrigger-construction entity instead of the hardcoded
// boatconstruction-sailed-oak that the base method spawns.
public class ItemOutriggerRollers : ItemRoller
{
    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        if (blockSel == null) return;
        var player = (byEntity as EntityPlayer)?.Player;

        if (slot.StackSize < 5)
        {
            (api as Vintagestory.API.Client.ICoreClientAPI)?.TriggerIngameError(
                this, "need5", Lang.Get("Need 5 outrigger rollers to place a boat construction site"));
            return;
        }
        if (!SuitableLocation(player, blockSel))
        {
            (api as Vintagestory.API.Client.ICoreClientAPI)?.TriggerIngameError(
                this, "unsuitableLocation",
                Lang.Get("Requires a suitable location near water to place a boat construction site. Boat will roll towards the blue highlighted area. Use tool mode to rotate"));
            return;
        }

        const string defaultMaterial = "oak";
        int orient = GetOrient(player);

        var type = byEntity.World.GetEntityType(
            new AssetLocation("seafarer", "outrigger-construction-" + defaultMaterial));
        if (type == null)
        {
            api.Logger.Error("[seafarer] outrigger-construction-{0} entity type not found.", defaultMaterial);
            return;
        }

        slot.TakeOut(5);
        slot.MarkDirty();

        var entity = byEntity.World.ClassRegistry.CreateEntity(type);
        entity.Pos.SetPos(blockSel.Position.ToVec3d().AddCopy(0.5, 1, 0.5));
        entity.Pos.Yaw = -GameMath.PIHALF + orient * GameMath.PIHALF;

        byEntity.World.SpawnEntity(entity);

        api.World.PlaySoundAt(new AssetLocation("sounds/block/planks"), byEntity, player);

        handling = EnumHandHandling.PreventDefault;
    }

    // Replicates the private SuitableLocation check from base ItemRoller —
    // base-game checks: solid ground below the site, free air above, water in front.
    // We delegate to the base game's siteListByFacing / waterEdgeByFacing static
    // tables which ItemRoller populates in OnLoaded — so they are valid by the
    // time this runs (item is loaded before any interaction).
    private bool SuitableLocation(IPlayer forPlayer, BlockSelection blockSel)
    {
        int orient = GetOrient(forPlayer);
        var siteList = siteListByFacing[orient];
        var waterEdgeList = waterEdgeByFacing[orient];

        var ba = api.World.BlockAccessor;
        bool placeable = true;
        var cpos = blockSel.Position;

        BlockPos minGround = siteList[0].AddCopy(0, 1, 0).Add(cpos);
        BlockPos maxGround = siteList[1].AddCopy(-1, 0, -1).Add(cpos);
        maxGround.Y = minGround.Y;

        ba.WalkBlocks(minGround, maxGround, (block, x, y, z) => {
            if (!block.SideIsSolid(new BlockPos(x, y, z), BlockFacing.UP.Index))
                placeable = false;
        });
        if (!placeable) return false;

        BlockPos minAir = siteList[0].AddCopy(0, 2, 0).Add(cpos);
        BlockPos maxAir = siteList[1].AddCopy(-1, 1, -1).Add(cpos);
        ba.WalkBlocks(minAir, maxAir, (block, x, y, z) => {
            var cboxes = block.GetCollisionBoxes(ba, new BlockPos(x, y, z));
            if (cboxes != null && cboxes.Length > 0) placeable = false;
        });

        BlockPos minWater = waterEdgeList[0].AddCopy(0, 1, 0).Add(cpos);
        BlockPos maxWater = waterEdgeList[1].AddCopy(-1, 0, -1).Add(cpos);
        WalkBlocks(minWater, maxWater, (block, x, y, z) => {
            if (!block.IsLiquid()) placeable = false;
        }, BlockLayersAccess.Fluid);

        return placeable;
    }
}
