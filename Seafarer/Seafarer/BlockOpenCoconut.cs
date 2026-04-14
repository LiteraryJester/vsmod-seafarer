using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SaltAndSand;

/// <summary>
/// A small liquid container (1L capacity) representing a cracked-open coconut half-shell.
/// When empty, right-clicking with a knife extracts coconut meat and destroys the block.
/// </summary>
public class BlockOpenCoconut : BlockLiquidContainerTopOpened
{
    protected override string meshRefsCacheKey => "openCoconutMeshRefs" + Code;

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (TryExtractMeat(world, byPlayer, blockSel))
            return true;

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    private bool TryExtractMeat(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot.Empty) return false;

        string? toolType = activeSlot.Itemstack.Collectible.Tool?.ToString().ToLowerInvariant();
        if (toolType != "knife") return false;

        // Only extract meat when the coconut is empty
        var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityOpenCoconut;
        if (be == null) return false;

        ItemStack? content = be.GetContent();
        if (content != null && content.StackSize > 0) return false;

        // Server-side: do the actual extraction
        if (world.Side == EnumAppSide.Server)
        {
            // Damage the knife
            activeSlot.Itemstack.Collectible.DamageItem(world, byPlayer.Entity, activeSlot);

            // Drop coconut meat
            Item? coconutMeat = world.GetItem(new AssetLocation("seafarer:coconutmeat"));
            if (coconutMeat != null)
            {
                ItemStack meatStack = new ItemStack(coconutMeat, 4);
                if (!byPlayer.InventoryManager.TryGiveItemstack(meatStack))
                {
                    world.SpawnItemEntity(meatStack, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }

            // Destroy the open coconut block
            world.BlockAccessor.SetBlock(0, blockSel.Position);
            world.PlaySoundAt(new AssetLocation("game:sounds/player/knap1"), byPlayer.Entity, byPlayer, true, 16f);
        }

        // Return true on both client and server so the interaction proceeds
        return true;
    }
}
