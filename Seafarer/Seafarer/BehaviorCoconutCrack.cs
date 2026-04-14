using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SaltAndSand;

/// <summary>
/// Custom ground-stored processable behavior for coconuts.
/// When shift+right-clicked with a knife or hammer, removes the coconut from ground storage
/// and places a BlockOpenCoconut liquid container pre-filled with coconut water.
/// </summary>
public class BehaviorCoconutCrack : CollectibleBehavior, IContainedInteractable
{
    private float processTime;
    private AssetLocation? processingSound;
    private string? outputBlockCode;
    private string? liquidCode;
    private int liquidAmount;

    public BehaviorCoconutCrack(CollectibleObject collObj) : base(collObj) { }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        processTime = properties["processTime"].AsFloat(2f);
        string? soundCode = properties["processingSound"].AsString();
        if (soundCode != null)
            processingSound = AssetLocation.Create(soundCode, collObj.Code.Domain);

        outputBlockCode = properties["outputBlockCode"].AsString("seafarer:opencoconut");
        liquidCode = properties["liquidCode"].AsString("seafarer:coconutwaterportion");
        liquidAmount = properties["liquidAmount"].AsInt(100); // 100 items = 1L at 100 items/litre
    }

    private bool IsValidTool(IPlayer byPlayer)
    {
        var tool = byPlayer.InventoryManager.ActiveTool;
        return tool == EnumTool.Knife || tool == EnumTool.Hammer;
    }

    public bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!IsValidTool(byPlayer)) return false;
        if (!byPlayer.Entity.Controls.ShiftKey) return false;
        if (!be.Api.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use)) return false;

        be.Api.World.PlaySoundAt(processingSound, blockSel.Position, 0, byPlayer);
        return true;
    }

    public bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!IsValidTool(byPlayer)) return false;
        if (!byPlayer.Entity.Controls.ShiftKey) return false;
        if (blockSel == null) return false;

        if (be.Api.World.Rand.NextDouble() < 0.05)
            be.Api.World.PlaySoundAt(processingSound, blockSel.Position, 0, byPlayer);

        return secondsUsed < processTime;
    }

    public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!IsValidTool(byPlayer)) return;
        if (secondsUsed < processTime - 0.05f) return;
        if (be.Api.World.Side != EnumAppSide.Server) return;

        BlockPos pos = blockSel.Position;

        // Damage the tool
        var toolSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        toolSlot.Itemstack?.Collectible.DamageItem(be.Api.World, byPlayer.Entity, toolSlot);

        // Remove the coconut from ground storage
        slot.Itemstack = null;
        be.MarkDirty(true);

        // Drop any remaining items in the ground storage before overwriting
        if (!be.Inventory.Empty)
        {
            foreach (var s in be.Inventory)
            {
                if (!s.Empty)
                    be.Api.World.SpawnItemEntity(s.Itemstack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                s.Itemstack = null;
            }
        }
        be.Api.World.BlockAccessor.SetBlock(0, pos);

        // Place the open coconut block
        Block? openCoconutBlock = be.Api.World.GetBlock(new AssetLocation(outputBlockCode!));
        if (openCoconutBlock == null) return;

        be.Api.World.BlockAccessor.SetBlock(openCoconutBlock.Id, pos);

        // The block entity auto-fills with coconut water on first placement
        // (handled in BlockEntityOpenCoconut.OnBlockPlaced)

        be.Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/knap1"), byPlayer.Entity, byPlayer, true, 16f);

        be.Api.World.Logger.Audit("{0} cracked a coconut at {1}.", byPlayer.PlayerName, pos);
    }

    public bool OnContainedInteractCancel(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
        return true;
    }

    public WorldInteraction[] GetContainedInteractionHelp(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        bool notProtected = true;
        if (be.Api.World.Claims != null && be.Api.World is IClientWorldAccessor clientWorld && clientWorld.Player?.WorldData.CurrentGameMode == EnumGameMode.Survival)
        {
            var resp = clientWorld.Claims.TestAccess(clientWorld.Player, blockSel.Position, EnumBlockAccessFlags.Use);
            if (resp != EnumWorldAccessResponse.Granted) notProtected = false;
        }

        if (notProtected)
        {
            return
            [
                new()
                {
                    ActionLangCode = "seafarer:groundstoredaction-crack",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "shift",
                    Itemstacks = ObjectCacheUtil.GetToolStacks(be.Api, EnumTool.Knife)
                        .Append(ObjectCacheUtil.GetToolStacks(be.Api, EnumTool.Hammer))
                }
            ];
        }

        return [];
    }
}
