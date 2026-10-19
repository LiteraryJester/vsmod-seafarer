using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

public class BlockGriddleHearth : Block, IIgnitable
{
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        PlacedPriorityInteract = true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var be = GetBlockEntity<BlockEntityGriddleHearth>(blockSel.Position);
        if (be != null)
        {
            // Shift + right-click with a torch/firestarter is the vanilla "ignite" gesture: it routes to
            // the held item's CanIgnite behavior, which drives the IIgnitable hooks below. Yield to it so
            // we don't swallow the interaction with our own (non-shift) ignite/fuel handling.
            if (byPlayer.Entity.Controls.ShiftKey && be.CanIgnite)
            {
                var held = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;
                bool fireSource = held != null
                    && (held.Block?.HasBehavior<BlockBehaviorCanIgnite>() == true || held.Collectible is ItemFirestarter);
                if (fireSource) return false;
            }

            return be.OnInteract(byPlayer, blockSel);
        }
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var be = GetBlockEntity<BlockEntityGriddleHearth>(blockSel.Position);
        if (be != null)
        {
            return be.OnIgniteStep(secondsUsed, byPlayer);
        }
        return false;
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var be = GetBlockEntity<BlockEntityGriddleHearth>(blockSel?.Position);
        be?.OnIgniteStop(secondsUsed, byPlayer);
    }

    public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
        var be = GetBlockEntity<BlockEntityGriddleHearth>(blockSel?.Position);
        be?.OnIgniteCancel();
        return true;
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        var be = GetBlockEntity<BlockEntityGriddleHearth>(pos);
        be?.DropContents(world, pos);
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    #region IIgnitable

    // Lit torch / firestarter held-interaction asks the targeted block whether it can be ignited.
    public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
    {
        var be = GetBlockEntity<BlockEntityGriddleHearth>(pos);
        if (be == null || !be.CanIgnite) return EnumIgniteState.NotIgnitablePreventDefault;
        return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
    }

    // Fired once the player has held the ignite long enough; actually light the hearth.
    public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;
        GetBlockEntity<BlockEntityGriddleHearth>(pos)?.Ignite();
    }

    // We don't let players light a torch off the hearth.
    EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
    {
        return EnumIgniteState.NotIgnitable;
    }

    #endregion
}
