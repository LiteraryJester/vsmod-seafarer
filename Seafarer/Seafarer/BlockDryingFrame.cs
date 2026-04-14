using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SaltAndSand;

public class BlockDryingFrame : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        return GetBlockEntity<BlockEntityDryingFrame>(blockSel.Position)?.OnInteract(byPlayer, blockSel)
            ?? base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
