using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Seafarer;

public class BlockGriddleHearth : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var be = GetBlockEntity<BlockEntityGriddleHearth>(blockSel.Position);
        if (be != null)
        {
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
}
