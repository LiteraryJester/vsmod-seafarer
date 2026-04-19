using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Seafarer;

public class BlockGrowPot : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var slot = byPlayer?.InventoryManager?.ActiveHotbarSlot;
        var held = slot?.Itemstack;

        if (held?.Block is BlockFruitTreeBranch ftBlock && ftBlock.Variant?["type"] == "cutting")
        {
            string? treeType = held.Attributes?.GetString("type");
            if (string.IsNullOrEmpty(treeType))
            {
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            // The pot's shape fills its whole block (tall planter), so the
            // cutting placed directly above sits visually on the soil inside
            // the pot with no gap. The pot itself persists as decoration.
            var plantPos = blockSel.Position.UpCopy();
            var cuttingBlock = world.GetBlock(new AssetLocation("game:fruittree-cutting"));
            if (cuttingBlock == null) return false;
            if (!world.BlockAccessor.GetBlock(plantPos).IsReplacableBy(cuttingBlock)) return false;

            if (world.Side == EnumAppSide.Server)
            {
                var dwarfStack = new ItemStack(cuttingBlock);
                dwarfStack.Attributes.SetString("type", treeType);
                dwarfStack.Attributes.SetBool("dwarf", true);

                // Lifecycle speed — each diff is clamped into the species's own
                // NatFloat range, so these push each value to (or near) its lower
                // bound. Ripe window is kept modest so the player still has time
                // to harvest before fruit rots.
                dwarfStack.Attributes.SetFloat("floweringDaysDiff", -3f);
                dwarfStack.Attributes.SetFloat("fruitingDaysDiff", -15f);
                dwarfStack.Attributes.SetFloat("ripeDaysDiff", -2f);
                dwarfStack.Attributes.SetFloat("growthStepDaysDiff", -2f);

                // Survival — tolerate much colder winters than the species would
                // naturally, slip dormancy at a lower warm-up temp, need less cold
                // to vernalize, and accept vernalization at warmer temps. Potted
                // trees are often kept indoors so the ambient is mild-to-warm.
                dwarfStack.Attributes.SetFloat("dieBelowTempDiff", -25f);
                dwarfStack.Attributes.SetFloat("enterDormancyTempDiff", -10f);
                dwarfStack.Attributes.SetFloat("leaveDormancyTempDiff", -10f);
                dwarfStack.Attributes.SetFloat("vernalizationHoursDiff", -200f);
                dwarfStack.Attributes.SetFloat("vernalizationTempDiff", 5f);

                world.BlockAccessor.SetBlock(cuttingBlock.BlockId, plantPos, dwarfStack);

                slot!.TakeOut(1);
                slot.MarkDirty();
                world.PlaySoundAt(new AssetLocation("game:sounds/block/plant"), plantPos.X, plantPos.Y, plantPos.Z, byPlayer);
            }

            return true;
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
