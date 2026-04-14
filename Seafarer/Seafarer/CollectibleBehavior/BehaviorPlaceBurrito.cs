using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Seafarer;

public class BehaviorPlaceBurrito : CollectibleBehavior
{
    public BehaviorPlaceBurrito(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling,
        ref EnumHandling handling)
    {
        if (blockSel == null || !firstEvent) return;
        if (byEntity is not EntityPlayer entityPlayer) return;

        // Check there's a solid surface below
        if (blockSel.Face != BlockFacing.UP) return;

        var world = byEntity.World;

        // Don't place on blocks that handle their own interactions (griddles, ovens, etc.)
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) != null) return;

        var placePos = blockSel.Position.AddCopy(blockSel.Face);

        // Check the position is free
        var existingBlock = world.BlockAccessor.GetBlock(placePos);
        if (existingBlock.Id != 0 && !existingBlock.IsReplacableBy(world.GetBlock(new AssetLocation("seafarer:burrito-raw")))) return;

        var burritoBlock = world.GetBlock(new AssetLocation("seafarer:burrito-raw"));
        if (burritoBlock == null) return;

        if (world.Side == EnumAppSide.Server)
        {
            world.BlockAccessor.SetBlock(burritoBlock.Id, placePos);
            slot.TakeOut(1);
            slot.MarkDirty();
        }

        world.PlaySoundAt(new AssetLocation("sounds/player/build"), byEntity, entityPlayer.Player, true, 16f);

        handHandling = EnumHandHandling.PreventDefault;
        handling = EnumHandling.PreventSubsequent;
    }
}
