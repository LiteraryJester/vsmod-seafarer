using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Seafarer;

public class BlockPrepTable : Block
{
    private BlockFacing GetRightDir(string side)
    {
        return side switch
        {
            "north" => BlockFacing.EAST,
            "south" => BlockFacing.WEST,
            "east" => BlockFacing.SOUTH,
            "west" => BlockFacing.NORTH,
            _ => BlockFacing.EAST
        };
    }

    private BlockPos[] GetAllPositions(BlockPos leftPos, string side)
    {
        BlockFacing rightDir = GetRightDir(side);
        return new[]
        {
            leftPos.AddCopy(rightDir.Opposite),  // platform
            leftPos.Copy(),                        // left
            leftPos.AddCopy(rightDir)              // right
        };
    }

    private bool IsPlatformPart(string part) => part.StartsWith("platform");

    private BlockPos GetLeftPos(BlockPos pos, string side, string part)
    {
        BlockFacing rightDir = GetRightDir(side);
        if (IsPlatformPart(part))
            return pos.AddCopy(rightDir);
        if (part == "right")
            return pos.AddCopy(rightDir.Opposite);
        return pos.Copy();
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack,
        BlockSelection blockSel, ref string failureCode)
    {
        // Let HorizontalOrientable place the correct left variant first
        if (!base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
            return false;

        // Read the actual facing from the placed block (HorizontalOrientable resolved it)
        Block placedBlock = world.BlockAccessor.GetBlock(blockSel.Position);
        string side = placedBlock.Variant["side"] ?? "north";

        BlockPos leftPos = blockSel.Position;
        var positions = GetAllPositions(leftPos, side);

        // Check platform and right positions are clear
        for (int i = 0; i < positions.Length; i++)
        {
            if (i == 1) continue;
            Block existing = world.BlockAccessor.GetBlock(positions[i]);
            if (existing.Id != 0 && !existing.IsReplacableBy(placedBlock))
            {
                // Not enough space — remove the left block we just placed
                world.BlockAccessor.SetBlock(0, leftPos);
                failureCode = "requirespace";
                return false;
            }
        }

        Block? platformBlock = world.GetBlock(new AssetLocation("seafarer:preptable-platform-empty-" + side));
        if (platformBlock != null)
            world.BlockAccessor.SetBlock(platformBlock.Id, positions[0]);

        Block? rightBlock = world.GetBlock(new AssetLocation("seafarer:preptable-right-" + side));
        if (rightBlock != null)
            world.BlockAccessor.SetBlock(rightBlock.Id, positions[2]);

        return true;
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer,
        float dropQuantityMultiplier = 1)
    {
        string side = Variant["side"] ?? "north";
        string part = Variant["part"] ?? "left";
        BlockPos leftPos = GetLeftPos(pos, side, part);
        var positions = GetAllPositions(leftPos, side);

        // Drop contents from platform and right entities
        for (int i = 0; i < positions.Length; i++)
        {
            var slotEntity = world.BlockAccessor.GetBlockEntity(positions[i]) as BlockEntityPrepTableSlot;
            slotEntity?.DropContents(world, positions[i]);
        }

        // Remove all other parts
        for (int i = 0; i < positions.Length; i++)
        {
            if (positions[i].Equals(pos)) continue;
            Block block = world.BlockAccessor.GetBlock(positions[i]);
            if (block is BlockPrepTable)
            {
                world.BlockAccessor.SetBlock(0, positions[i]);
            }
        }

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    private const float CraftTime = 1.5f; // seconds to hold right-click

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        // Platform and right handle their own attachment interactions
        var slotEntity = GetBlockEntity<BlockEntityPrepTableSlot>(blockSel.Position);
        if (slotEntity != null)
        {
            return slotEntity.OnInteract(byPlayer, blockSel);
        }

        // Left (work area) — check recipe on both sides before starting effects
        var prepEntity = GetBlockEntity<BlockEntityPrepTable>(blockSel.Position);
        if (prepEntity != null && prepEntity.CanCraft(byPlayer))
        {
            var sound = new AssetLocation(prepEntity.LastRecipeSound ?? "game:sounds/player/knap1");
            world.PlaySoundAt(sound,
                blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5,
                byPlayer, true, 16f);
            return true;
        }

        return false;
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var prepEntity = GetBlockEntity<BlockEntityPrepTable>(blockSel?.Position);
        if (prepEntity == null) return false;

        // Stop if held item is gone or no inventory space
        if (byPlayer.InventoryManager.ActiveHotbarSlot.Empty) return false;

        // Trigger knife/attack animation on the player's hand
        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);

        // Spawn small particles on the work surface
        if (world.Side == EnumAppSide.Client && world.Rand.NextDouble() < 0.3)
        {
            ItemStack? heldStack = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;
            if (heldStack != null && blockSel != null)
            {
                Vec3d pos = blockSel.Position.ToVec3d().Add(0.5, 0.7, 0.5);
                world.SpawnCubeParticles(pos, heldStack, 0.2f, 2, 0.4f, byPlayer, new Vec3f(0, 0.5f, 0));
            }
        }

        return secondsUsed < CraftTime;
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (blockSel == null) return;
        if (secondsUsed < CraftTime - 0.05f) return;

        var prepEntity = GetBlockEntity<BlockEntityPrepTable>(blockSel.Position);
        if (prepEntity == null) return;

        if (prepEntity.OnInteract(byPlayer, blockSel))
        {
            var sound = new AssetLocation(prepEntity.LastRecipeSound ?? "game:sounds/player/knap1");
            world.PlaySoundAt(sound,
                blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5,
                byPlayer, true, 16f);
        }
    }
}
