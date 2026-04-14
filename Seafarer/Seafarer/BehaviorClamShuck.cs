using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SaltAndSand;

public class BehaviorClamShuck : CollectibleBehavior, IContainedInteractable
{
    private const float ProcessTime = 1.5f;

    public BehaviorClamShuck(CollectibleObject collObj) : base(collObj) { }

    private static bool IsKnife(IPlayer byPlayer)
    {
        return byPlayer.InventoryManager.ActiveTool == EnumTool.Knife;
    }

    public bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!IsKnife(byPlayer)) return false;
        if (!byPlayer.Entity.Controls.ShiftKey) return false;

        be.Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/knap1"), blockSel.Position, 0, byPlayer);
        return true;
    }

    public bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!IsKnife(byPlayer)) return false;
        if (!byPlayer.Entity.Controls.ShiftKey) return false;

        return secondsUsed < ProcessTime;
    }

    public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!IsKnife(byPlayer)) return;
        if (secondsUsed < ProcessTime - 0.05f) return;
        if (be.Api.World.Side != EnumAppSide.Server) return;

        var world = be.Api.World;
        BlockPos pos = blockSel.Position;

        // Damage the knife
        var toolSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        toolSlot.Itemstack?.Collectible.DamageItem(world, byPlayer.Entity, toolSlot);

        // Remove the clam from ground storage
        slot.Itemstack = null;
        be.MarkDirty(true);

        // If ground storage is now empty, remove the block entirely
        if (be.Inventory.Empty)
        {
            world.BlockAccessor.SetBlock(0, pos);
        }

        // Drop 1 premium fish meat
        var fishItem = world.GetItem(new AssetLocation("seafarer:premiumfish-raw"));
        if (fishItem != null)
        {
            var meatStack = new ItemStack(fishItem, 1);
            if (!byPlayer.InventoryManager.TryGiveItemstack(meatStack))
                world.SpawnItemEntity(meatStack, pos.ToVec3d().Add(0.5, 0.2, 0.5));
        }

        // Drop 1 clam shell (random color)
        string[] colors = ["latte", "plain", "seafoam", "darkpurple", "cinnamon", "turquoise"];
        string color = colors[world.Rand.Next(colors.Length)];
        var shellBlock = world.GetBlock(new AssetLocation($"game:seashell-clam-{color}"));
        if (shellBlock != null)
        {
            var shellStack = new ItemStack(shellBlock, 1);
            if (!byPlayer.InventoryManager.TryGiveItemstack(shellStack))
                world.SpawnItemEntity(shellStack, pos.ToVec3d().Add(0.5, 0.2, 0.5));
        }

        world.PlaySoundAt(new AssetLocation("game:sounds/player/knap1"),
            byPlayer.Entity, byPlayer, true, 16f);
    }

    public bool OnContainedInteractCancel(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
        return true;
    }

    public WorldInteraction[] GetContainedInteractionHelp(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        return
        [
            new()
            {
                ActionLangCode = "seafarer:groundstoredaction-shuck",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "shift",
                Itemstacks = ObjectCacheUtil.GetToolStacks(be.Api, EnumTool.Knife)
            }
        ];
    }
}
