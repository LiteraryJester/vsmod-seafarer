using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Seafarer;

public class BehaviorShellCrush : CollectibleBehavior, IContainedInteractable
{
    private const float ProcessTime = 1f;

    public BehaviorShellCrush(CollectibleObject collObj) : base(collObj) { }

    private static bool IsValidTool(IPlayer byPlayer)
    {
        var tool = byPlayer.InventoryManager.ActiveTool;
        return tool == EnumTool.Hammer;
    }

    private static bool IsHoldingStone(IPlayer byPlayer)
    {
        var activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot.Empty) return false;
        return activeSlot.Itemstack.Collectible.Code.Path.StartsWith("stone-");
    }

    private static bool CanCrush(IPlayer byPlayer)
    {
        return IsValidTool(byPlayer) || IsHoldingStone(byPlayer);
    }

    public bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!CanCrush(byPlayer)) return false;
        if (!byPlayer.Entity.Controls.ShiftKey) return false;

        be.Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/knap1"), blockSel.Position, 0, byPlayer);
        return true;
    }

    public bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!CanCrush(byPlayer)) return false;
        if (!byPlayer.Entity.Controls.ShiftKey) return false;

        return secondsUsed < ProcessTime;
    }

    public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!CanCrush(byPlayer)) return;
        if (secondsUsed < ProcessTime - 0.05f) return;
        if (be.Api.World.Side != EnumAppSide.Server) return;

        var world = be.Api.World;
        BlockPos pos = blockSel.Position;

        // Damage the tool
        var toolSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        toolSlot.Itemstack?.Collectible.DamageItem(world, byPlayer.Entity, toolSlot);

        // Remove the shell from ground storage
        slot.Itemstack = null;
        be.MarkDirty(true);

        if (be.Inventory.Empty)
        {
            world.BlockAccessor.SetBlock(0, pos);
        }

        // Give crushed shell
        var crushedItem = world.GetItem(new AssetLocation("seafarer:crushedshell"));
        if (crushedItem != null)
        {
            var stack = new ItemStack(crushedItem, 1);
            if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                world.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0.2, 0.5));
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
                ActionLangCode = "seafarer:groundstoredaction-crush",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "shift",
                Itemstacks = ObjectCacheUtil.GetToolStacks(be.Api, EnumTool.Hammer)
            }
        ];
    }
}
