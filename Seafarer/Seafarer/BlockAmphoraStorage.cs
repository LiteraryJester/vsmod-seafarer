using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

public class BlockAmphoraStorage : BlockGenericTypedContainer
{
    public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
    {
        return Lang.Get("seafarer:block-amphora-storage");
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        var stack = new ItemStack(this);
        var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityAmphoraStorage;
        stack.Attributes.SetString("type", be?.type ?? "normal");
        if (be?.Sealed == true)
        {
            stack.Attributes.SetBool("sealed", true);
        }
        return stack;
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        return new[] { OnPickBlock(world, pos) };
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var be = GetBlockEntity<BlockEntityAmphoraStorage>(blockSel.Position);
        if (be == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        var handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

        // Seal with beeswax/fat
        if (handSlot.Itemstack?.Collectible.Attributes?["canSealCrock"]?.AsBool() == true)
        {
            if (!be.Sealed && be.HasContents())
            {
                if (api.Side == EnumAppSide.Server)
                {
                    be.Sealed = true;
                    handSlot.TakeOut(1);
                    handSlot.MarkDirty();
                    be.MarkDirty(true);
                    api.World.PlaySoundAt(new AssetLocation("sounds/player/seal"), byPlayer.Entity, byPlayer, true, 16f);
                }
            }
            else if (api.Side == EnumAppSide.Client)
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "amphoraemptyorsealed", Lang.Get("seafarer:amphora-empty-or-sealed"));
            }
            return true;
        }

        // If sealed, prevent opening or allow unseal
        if (be.Sealed)
        {
            if (byPlayer.Entity.Controls.ShiftKey)
            {
                if (api.Side == EnumAppSide.Server)
                {
                    be.Sealed = false;
                    be.MarkDirty(true);
                    api.World.PlaySoundAt(new AssetLocation("sounds/effect/seal-break"), byPlayer.Entity, null, true, 16f);
                }
                return true;
            }

            if (api.Side == EnumAppSide.Client)
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "amphorasealed", Lang.Get("seafarer:amphora-is-sealed"));
            }
            return true;
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    // Spoilage modifier is applied via BlockEntityAmphoraStorage.OnAcquireTransitionSpeed

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        if (BlockAmphora.IsSealed(inSlot.Itemstack))
        {
            dsc.AppendLine("<font color=\"#88cc88\">" + Lang.Get("seafarer:amphora-sealed-status") + "</font>");
        }
    }
}
