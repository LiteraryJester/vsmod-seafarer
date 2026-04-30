using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Seafarer;

public class BlockSaltPan : Block
{
    private const float BareHandTime = 5f;

    private static float GetHarvestTime(IPlayer byPlayer)
    {
        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot.Empty) return BareHandTime;

        string code = activeSlot.Itemstack.Collectible.Code.Path;
        int tier = activeSlot.Itemstack.Collectible.ToolTier;

        bool isShovel = code.StartsWith("shovel-");
        bool isKnife = code.StartsWith("knife-") || code.StartsWith("cleaver-");

        if (isShovel)
        {
            return tier switch
            {
                0 => 1.25f,  // stone
                1 => 1f,     // copper
                2 => 0.75f,  // bronze
                3 => 0.5f,   // iron
                4 => 0.15f,  // steel
                _ => 1.25f
            };
        }

        if (isKnife)
        {
            return tier switch
            {
                0 => 2.5f,   // stone
                1 => 2f,     // copper
                2 => 1.5f,   // bronze
                3 => 1.25f,  // iron
                4 => 1.25f,  // steel
                _ => 2.5f
            };
        }

        return BareHandTime;
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack,
        BlockSelection blockSel, ref string failureCode)
    {
        if (blockSel.Face != BlockFacing.UP)
        {
            failureCode = "requiresolidground";
            return false;
        }

        BlockPos belowPos = blockSel.Position.AddCopy(BlockFacing.DOWN);
        Block belowBlock = world.BlockAccessor.GetBlock(belowPos);
        if (!belowBlock.SideSolid[BlockFacing.UP.Index])
        {
            failureCode = "requiresolidground";
            return false;
        }

        return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var entity = GetBlockEntity<BlockEntitySaltPan>(blockSel.Position);
        if (entity == null) return false;

        // Salt meat takes priority over harvest when holding a valid meat item
        if (entity.CanHarvest())
        {
            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            bool holdingMeat = !activeSlot.Empty &&
                activeSlot.Itemstack.Collectible.Attributes?["saltRubbedOutput"]?.Exists == true;

            if (holdingMeat)
            {
                return entity.OnInteract(byPlayer, blockSel);
            }

            world.PlaySoundAt(new AssetLocation("game:sounds/block/gravel"),
                blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5,
                byPlayer, true, 16f);
            return true;
        }

        // Otherwise delegate to entity for other interactions (fill)
        return entity.OnInteract(byPlayer, blockSel);
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var entity = GetBlockEntity<BlockEntitySaltPan>(blockSel?.Position);
        if (entity == null || !entity.CanHarvest()) return false;

        float harvestTime = GetHarvestTime(byPlayer);

        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);

        // Spawn particles
        if (world.Side == EnumAppSide.Client && world.Rand.NextDouble() < 0.2)
        {
            if (blockSel == null) return secondsUsed < harvestTime;
            Vec3d pos = blockSel.Position.ToVec3d().Add(0.5, 0.3, 0.5);
            var saltItem = world.GetItem(new AssetLocation("game:salt"));
            if (saltItem != null)
            {
                world.SpawnCubeParticles(pos, new ItemStack(saltItem), 0.15f, 3, 0.3f, byPlayer, new Vec3f(0, 0.3f, 0));
            }
        }

        return secondsUsed < harvestTime;
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (blockSel == null) return;

        var entity = GetBlockEntity<BlockEntitySaltPan>(blockSel.Position);
        if (entity == null || !entity.CanHarvest()) return;

        float harvestTime = GetHarvestTime(byPlayer);
        if (secondsUsed < harvestTime - 0.05f) return;

        if (entity.TryHarvest(byPlayer))
        {
            world.PlaySoundAt(new AssetLocation("game:sounds/player/collect"),
                blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5,
                byPlayer, true, 16f);
        }
    }
}
