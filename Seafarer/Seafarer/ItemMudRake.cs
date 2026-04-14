using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Seafarer;

public class ItemMudRake : Item
{
    private static JsonObject? GetRakingDrops(Block block)
    {
        var drops = block.Attributes?["rakingDrops"];
        return drops != null && drops.Exists ? drops : null;
    }

    private float GetDropRateMultiplier()
    {
        return Attributes?["dropRateMultiplier"].AsFloat(1f) ?? 1f;
    }

    public override bool OnBlockBrokenWith(
        IWorldAccessor world, Entity byEntity, ItemSlot itemslot,
        BlockSelection blockSel, float dropQuantityMultiplier = 1)
    {
        var block = world.BlockAccessor.GetBlock(blockSel.Position);
        var dropsJson = GetRakingDrops(block);

        // If rakeable, suppress normal block drops (destroy instead of drop)
        float actualDropMultiplier = dropsJson != null ? 0f : dropQuantityMultiplier;
        bool result = base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, actualDropMultiplier);

        // If block had raking drops, roll the table multiple times
        if (dropsJson != null && world.Side == EnumAppSide.Server)
        {
            var cfg = SeafarerModSystem.MudRakeConfig;
            float multiplier = GetDropRateMultiplier();

            for (int i = 0; i < cfg.DropRollsPerBlock; i++)
            {
                RollDrops(world, byEntity, dropsJson, multiplier, blockSel.Position);
            }
        }

        return result;
    }

    private void RollDrops(IWorldAccessor world, Entity byEntity, JsonObject dropsArray, float multiplier, BlockPos pos)
    {
        if (dropsArray.Token == null) return;
        foreach (var dropToken in dropsArray.Token)
        {
            var drop = new JsonObject(dropToken);
            float chance = drop["chance"]["avg"].AsFloat(0) * multiplier;

            if (world.Rand.NextDouble() > chance) continue;

            string type = drop["type"].AsString("item");
            string? code = drop["code"].AsString();
            if (string.IsNullOrEmpty(code)) continue;

            ItemStack? dropStack = null;
            if (type == "block")
            {
                var blocks = world.SearchBlocks(new AssetLocation(code));
                if (blocks != null && blocks.Length > 0)
                {
                    var chosen = blocks[world.Rand.Next(blocks.Length)];
                    dropStack = new ItemStack(chosen, 1);
                }
            }
            else
            {
                var items = world.SearchItems(new AssetLocation(code));
                if (items != null && items.Length > 0)
                {
                    var chosen = items[world.Rand.Next(items.Length)];
                    dropStack = new ItemStack(chosen, 1);
                }
            }

            if (dropStack == null) continue;

            world.SpawnItemEntity(dropStack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }
    }
}
