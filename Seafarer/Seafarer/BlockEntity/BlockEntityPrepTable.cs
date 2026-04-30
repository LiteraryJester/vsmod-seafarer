using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

/// <summary>
/// Left (center) block entity — the work area.
/// Handles prep table recipe interactions by reading state from adjacent platform and right blocks.
/// </summary>
public class BlockEntityPrepTable : BlockEntity
{
    private BlockFacing GetRightDir()
    {
        string side = Block?.Variant?["side"] ?? "north";
        return side switch
        {
            "north" => BlockFacing.EAST,
            "south" => BlockFacing.WEST,
            "east" => BlockFacing.SOUTH,
            "west" => BlockFacing.NORTH,
            _ => BlockFacing.EAST
        };
    }

    private BlockEntityPrepTablePlatform? GetPlatformEntity()
    {
        BlockPos platformPos = Pos.AddCopy(GetRightDir().Opposite);
        return Api.World.BlockAccessor.GetBlockEntity(platformPos) as BlockEntityPrepTablePlatform;
    }

    private BlockEntityPrepTableRight? GetRightEntity()
    {
        BlockPos rightPos = Pos.AddCopy(GetRightDir());
        return Api.World.BlockAccessor.GetBlockEntity(rightPos) as BlockEntityPrepTableRight;
    }

    /// <summary>Check if a recipe can be crafted without executing it. Used for InteractStart.
    /// Also stores the matched recipe's sound in LastRecipeSound.</summary>
    internal bool CanCraft(IPlayer byPlayer)
    {
        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot.Empty) return false;

        if (!HasInventorySpace(byPlayer)) return false;

        var registry = Api.ModLoader.GetModSystem<PrepTableRecipeRegistry>();
        if (registry == null) return false;

        var platform = GetPlatformEntity();
        var right = GetRightEntity();

        int barrelSalt = platform?.GetBarrelSaltCount() ?? 0;
        ItemStack? barrelLiquid = platform?.GetBarrelLiquidContent();
        ItemStack? toolSlotItem = right?.GetToolItem();

        var recipe = registry.FindMatch(activeSlot.Itemstack, barrelSalt, barrelLiquid, toolSlotItem);
        if (recipe != null)
        {
            LastRecipeSound = recipe.Sound;
            return true;
        }
        return false;
    }

    private bool HasInventorySpace(IPlayer byPlayer)
    {
        // Check hotbar and backpack for any empty slot
        var hotbar = byPlayer.InventoryManager.GetOwnInventory("hotbar");
        if (hotbar != null)
        {
            for (int i = 0; i < hotbar.Count; i++)
            {
                if (hotbar[i].Empty) return true;
            }
        }

        var backpack = byPlayer.InventoryManager.GetOwnInventory("backpack");
        if (backpack != null)
        {
            for (int i = 0; i < backpack.Count; i++)
            {
                if (backpack[i].Empty) return true;
            }
        }

        return false;
    }

    /// <summary>The sound from the last completed recipe, for the block class to play on the client.</summary>
    internal string? LastRecipeSound { get; private set; }

    internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
    {
        if (Api.Side != EnumAppSide.Server) return true;

        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot.Empty) return false;

        var registry = Api.ModLoader.GetModSystem<PrepTableRecipeRegistry>();
        if (registry == null) return false;

        // Read state from adjacent blocks
        var platform = GetPlatformEntity();
        var right = GetRightEntity();

        int barrelSalt = platform?.GetBarrelSaltCount() ?? 0;
        ItemStack? barrelLiquid = platform?.GetBarrelLiquidContent();
        ItemStack? toolSlotItem = right?.GetToolItem();

        // Find matching recipe
        var recipe = registry.FindMatch(activeSlot.Itemstack, barrelSalt, barrelLiquid, toolSlotItem);
        if (recipe == null) return false;

        // Resolve output
        string outputCode = registry.ResolveOutputCode(recipe, activeSlot.Itemstack);
        CollectibleObject? outputCollectible = recipe.Output.Type == "block"
            ? Api.World.GetBlock(new AssetLocation(outputCode))
            : Api.World.GetItem(new AssetLocation(outputCode));

        if (outputCollectible == null)
        {
            Api.Logger.Warning("PrepTable: Output item not found: {0}", outputCode);
            return false;
        }

        // Give output first, before consuming inputs
        var outputStack = new ItemStack(outputCollectible, recipe.Output.Quantity);
        if (recipe.Output.LiquidFill != null && outputCollectible is BlockLiquidContainerBase liquidContainer)
        {
            var liquidItem = Api.World.GetItem(new AssetLocation(recipe.Output.LiquidFill.Code));
            if (liquidItem != null)
            {
                var liquidStack = new ItemStack(liquidItem, 1);
                liquidContainer.TryPutLiquid(outputStack, liquidStack, recipe.Output.LiquidFill.Litres);
            }
        }
        if (!byPlayer.InventoryManager.TryGiveItemstack(outputStack))
        {
            Api.World.SpawnItemEntity(outputStack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
        }

        // Give extra outputs
        if (recipe.ExtraOutputs != null)
        {
            foreach (var extra in recipe.ExtraOutputs)
            {
                CollectibleObject? extraCollectible = extra.Type == "block"
                    ? Api.World.GetBlock(new AssetLocation(extra.Code))
                    : Api.World.GetItem(new AssetLocation(extra.Code));
                if (extraCollectible == null) continue;

                var extraStack = new ItemStack(extraCollectible, extra.Quantity);
                if (!byPlayer.InventoryManager.TryGiveItemstack(extraStack))
                {
                    Api.World.SpawnItemEntity(extraStack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
                }
            }
        }

        // Consume input
        activeSlot.TakeOut(recipe.Input.Quantity);
        activeSlot.MarkDirty();

        // Consume barrel contents
        if (recipe.BarrelConsumes != null && platform != null)
        {
            platform.ConsumeBarrelItem(recipe.BarrelConsumes.Code, recipe.BarrelConsumes.Quantity, registry);
        }

        // Store the recipe sound so the block class can play it client-side
        LastRecipeSound = recipe.Sound;

        return true;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);
        sb.AppendLine(Lang.Get("seafarer:preptable-workarea"));
    }
}
