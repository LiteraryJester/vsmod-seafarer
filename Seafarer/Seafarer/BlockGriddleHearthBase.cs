using Vintagestory.API.Common;

namespace SaltAndSand;

/// <summary>
/// The empty stone hearth base. When a griddle is placed on it,
/// it transforms into the combined griddlehearth-{material} block.
/// </summary>
public class BlockGriddleHearthBase : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot.Empty) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        var heldItem = activeSlot.Itemstack;
        if (heldItem?.Block is BlockGriddle griddle)
        {
            // Replace this base block with the combined hearth block, preserving orientation
            string material = griddle.GetMaterial();
            string side = Variant["side"] ?? "north";
            var combinedBlock = world.GetBlock(new AssetLocation("seafarer:griddlehearth-" + side + "-" + material));
            if (combinedBlock != null)
            {
                world.BlockAccessor.SetBlock(combinedBlock.Id, blockSel.Position);

                // Set the griddle material on the new entity
                var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGriddleHearth;
                be?.SetGriddleMaterial(material);

                activeSlot.TakeOut(1);
                activeSlot.MarkDirty();

                world.PlaySoundAt(new AssetLocation("sounds/block/ceramics"), byPlayer.Entity, byPlayer, true, 16f);
                return true;
            }
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
