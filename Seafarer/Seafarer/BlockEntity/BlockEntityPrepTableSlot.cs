using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

/// <summary>
/// A single-slot display entity for prep table attachment points.
/// Renders the attached item on the block surface.
/// Subclasses define validation and transform position.
/// </summary>
public abstract class BlockEntityPrepTableSlot : BlockEntityDisplay
{
    private InventoryGeneric inventory;

    public override InventoryBase Inventory => inventory;
    public override string InventoryClassName => SlotClassName;

    protected abstract string SlotClassName { get; }
    protected abstract string SlotLabel { get; }
    protected abstract int AttachmentSelBox { get; }
    protected abstract bool IsValidItem(ItemStack stack);

    public BlockEntityPrepTableSlot()
    {
        inventory = new InventoryDisplayed(this, 1, "preptableslot-0", null);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
    }

    internal virtual bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
    {
        if (blockSel.SelectionBoxIndex != AttachmentSelBox) return false;

        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

        // Sneak+click = place/remove the attachment
        if (byPlayer.Entity.Controls.ShiftKey)
        {
            if (activeSlot.Empty)
                return TryTake(byPlayer);
            return TryPut(activeSlot, byPlayer);
        }

        // Normal click on empty slot = try to place
        if (Inventory[0].Empty)
        {
            if (!activeSlot.Empty)
                return TryPut(activeSlot, byPlayer);
            return false;
        }

        // Normal click when slot has item = interact with the stored item
        return OnInteractWithStoredItem(byPlayer, activeSlot);
    }

    /// <summary>Override to add interaction with the stored item (e.g., liquid transfer).</summary>
    protected virtual bool OnInteractWithStoredItem(IPlayer byPlayer, ItemSlot activeSlot)
    {
        return false;
    }

    private bool TryPut(ItemSlot slot, IPlayer byPlayer)
    {
        if (!Inventory[0].Empty) return false;
        if (slot.Itemstack == null || !IsValidItem(slot.Itemstack)) return false;

        if (slot.TryPutInto(Api.World, Inventory[0], 1) > 0)
        {
            updateMesh(0);
            Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/build"), byPlayer.Entity, byPlayer, true, 16f);
            MarkDirty(true);
            return true;
        }
        return false;
    }

    private bool TryTake(IPlayer byPlayer)
    {
        if (Inventory[0].Empty) return false;

        ItemStack stack = Inventory[0].TakeOut(1);
        if (byPlayer.InventoryManager.TryGiveItemstack(stack))
        {
            Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/collect"), byPlayer.Entity, byPlayer, true, 16f);
        }
        if (stack.StackSize > 0)
        {
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }
        updateMesh(0);
        MarkDirty(true);
        return true;
    }

    public void DropContents(IWorldAccessor world, BlockPos pos)
    {
        if (!Inventory[0].Empty)
        {
            world.SpawnItemEntity(Inventory[0].TakeOutWhole(), pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);
        if (forPlayer?.CurrentBlockSelection == null) return;
        if (forPlayer.CurrentBlockSelection.SelectionBoxIndex != AttachmentSelBox) return;

        string name = !Inventory[0].Empty
            ? Inventory[0].Itemstack?.GetName() ?? ""
            : Lang.Get("seafarer:preptable-slot-empty");
        sb.AppendLine(SlotLabel + ": " + name);
    }
}

/// <summary>Platform block entity — holds a barrel. Supports liquid transfer.</summary>
public class BlockEntityPrepTablePlatform : BlockEntityPrepTableSlot
{
    protected override string SlotClassName => "preptable-platform";
    protected override string SlotLabel => Lang.Get("seafarer:preptable-slot-barrel");
    protected override int AttachmentSelBox => 0; // Platform: 0=barrel slot, 1=body

    protected override bool IsValidItem(ItemStack stack) =>
        stack?.Collectible?.Code?.Path?.Contains("barrel") == true ||
        stack?.Collectible?.Code?.Path?.Contains("ongii") == true;

    private const int MaxSaltCapacity = 64;

    /// <summary>Get the salt count stored in the barrel's itemstack attributes.</summary>
    private int GetBarrelSalt()
    {
        return Inventory[0].Itemstack?.Attributes?.GetInt("saltCount", 0) ?? 0;
    }

    private void SetBarrelSalt(int count)
    {
        var stack = Inventory[0].Itemstack;
        if (stack == null) return;
        stack.Attributes.SetInt("saltCount", count);
    }

    protected override bool OnInteractWithStoredItem(IPlayer byPlayer, ItemSlot activeSlot)
    {
        if (Api.Side != EnumAppSide.Server) return true;

        var barrelStack = Inventory[0].Itemstack;
        if (barrelStack == null) return false;

        var heldStack = activeSlot.Itemstack;

        // Empty hand — take salt out if any
        if (heldStack == null)
        {
            return TryTakeSalt(byPlayer);
        }

        // Holding salt — put it in
        if (heldStack.Collectible.Code.Path == "salt")
        {
            return TryPutSalt(byPlayer, activeSlot);
        }

        // Holding a liquid container — do liquid transfer
        var barrelContainer = barrelStack.Collectible as BlockLiquidContainerBase;
        if (barrelContainer == null) return false;

        if (heldStack.Collectible is not ILiquidSource heldSource) return false;
        if (heldStack.Collectible is not ILiquidSink heldSink) return false;
        var heldContainer = heldStack.Collectible as BlockLiquidContainerBase;
        if (heldContainer == null) return false;

        var heldContent = heldContainer.GetContent(heldStack);
        var barrelContent = barrelContainer.GetContent(barrelStack);

        if (heldContent != null)
        {
            float litres = heldSource.CapacityLitres;
            int moved = barrelContainer.TryPutLiquid(barrelStack, heldContent, litres);
            if (moved > 0)
            {
                heldContainer.SplitStackAndPerformAction(byPlayer.Entity, activeSlot, (stack) =>
                {
                    heldContainer.TryTakeContent(stack, moved);
                    return moved;
                });

                Inventory[0].MarkDirty();
                MarkDirty(true);
                Api.World.PlaySoundAt(new AssetLocation("game:sounds/environment/waterfill"),
                    byPlayer.Entity, byPlayer, true, 16f);
                return true;
            }
        }
        else if (barrelContent != null)
        {
            float litres = heldSink.CapacityLitres;
            int moved = heldContainer.SplitStackAndPerformAction(byPlayer.Entity, activeSlot,
                (stack) => heldContainer.TryPutLiquid(stack, barrelContent, litres));
            if (moved > 0)
            {
                barrelContainer.TryTakeContent(barrelStack, moved);
                Inventory[0].MarkDirty();
                MarkDirty(true);
                Api.World.PlaySoundAt(new AssetLocation("game:sounds/environment/water-pour"),
                    byPlayer.Entity, byPlayer, true, 16f);
                return true;
            }
        }

        return false;
    }

    private bool TryPutSalt(IPlayer byPlayer, ItemSlot activeSlot)
    {
        int current = GetBarrelSalt();
        if (current >= MaxSaltCapacity) return false;

        int space = MaxSaltCapacity - current;
        int toAdd = System.Math.Min(space, activeSlot.Itemstack?.StackSize ?? 0);
        if (toAdd <= 0) return false;

        activeSlot.TakeOut(toAdd);
        activeSlot.MarkDirty();

        SetBarrelSalt(current + toAdd);
        Inventory[0].MarkDirty();
        MarkDirty(true);

        Api.World.PlaySoundAt(new AssetLocation("game:sounds/block/sand"),
            byPlayer.Entity, byPlayer, true, 16f);
        return true;
    }

    private bool TryTakeSalt(IPlayer byPlayer)
    {
        int current = GetBarrelSalt();
        if (current <= 0) return false;

        var saltItem = Api.World.GetItem(new AssetLocation("game:salt"));
        if (saltItem == null) return false;

        int toTake = System.Math.Min(current, saltItem.MaxStackSize);
        var saltStack = new ItemStack(saltItem, toTake);

        if (!byPlayer.InventoryManager.TryGiveItemstack(saltStack))
        {
            Api.World.SpawnItemEntity(saltStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }

        SetBarrelSalt(current - toTake);
        Inventory[0].MarkDirty();
        MarkDirty(true);

        Api.World.PlaySoundAt(new AssetLocation("game:sounds/block/gravel"),
            byPlayer.Entity, byPlayer, true, 16f);
        return true;
    }

    /// <summary>Swap platform block to show empty barrel or sealed (has contents).</summary>
    private void UpdateContentVisual()
    {
        if (Api?.Side != EnumAppSide.Server) return;

        string side = Block?.Variant?["side"] ?? "north";
        string currentPart = Block?.Variant?["part"] ?? "platform-empty";

        // Check if barrel has any contents (liquid or salt)
        bool hasContents = false;
        var barrelStack = Inventory[0].Itemstack;
        if (barrelStack != null)
        {
            var barrelContainer = barrelStack.Collectible as BlockLiquidContainerBase;
            hasContents = (barrelContainer != null && barrelContainer.GetContent(barrelStack) != null)
                       || GetBarrelSalt() > 0;
        }

        string targetState = hasContents ? "platform-filled" : "platform-empty";

        if (currentPart != targetState)
        {
            string blockCode = $"seafarer:preptable-{targetState}-{side}";
            Block? newBlock = Api.World.GetBlock(new AssetLocation(blockCode));
            if (newBlock != null)
            {
                Api.World.BlockAccessor.ExchangeBlock(newBlock.Id, Pos);
                this.Block = newBlock;

                // Play lid open/close sound
                string sound = hasContents ? "sounds/block/barrelclose" : "sounds/block/barrelopen";
                Api.World.PlaySoundAt(new AssetLocation(sound), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, true, 16f);
            }
        }
    }

    internal override bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
    {
        bool result = base.OnInteract(byPlayer, blockSel);
        if (result) UpdateContentVisual();
        return result;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);

        // Show barrel contents
        if (forPlayer?.CurrentBlockSelection?.SelectionBoxIndex != AttachmentSelBox) return;
        var barrelStack = Inventory[0].Itemstack;
        if (barrelStack == null) return;

        var barrelContainer = barrelStack.Collectible as BlockLiquidContainerBase;
        if (barrelContainer == null) return;

        // Show liquid contents
        var content = barrelContainer.GetContent(barrelStack);
        if (content != null)
        {
            var props = BlockLiquidContainerBase.GetContainableProps(content);
            if (props != null)
            {
                float litres = content.StackSize / props.ItemsPerLitre;
                sb.AppendLine(Lang.Get("seafarer:preptable-barrel-contents",
                    content.GetName(), litres));
            }
        }

        // Show salt count
        int salt = GetBarrelSalt();
        if (salt > 0)
        {
            sb.AppendLine(Lang.Get("seafarer:preptable-barrel-salt", salt, MaxSaltCapacity));
        }

        if (content == null && salt == 0)
        {
            sb.AppendLine(Lang.Get("seafarer:preptable-barrel-empty-contents"));
        }
    }

    // --- Public accessors for recipe system ---

    public int GetBarrelSaltCount() => GetBarrelSalt();

    public ItemStack? GetBarrelLiquidContent()
    {
        var barrelStack = Inventory[0].Itemstack;
        if (barrelStack == null) return null;
        var container = barrelStack.Collectible as BlockLiquidContainerBase;
        return container?.GetContent(barrelStack);
    }

    /// <summary>Consume an item from the barrel by code. Checks salt storage first, then liquid.</summary>
    public void ConsumeBarrelItem(string code, int quantity, PrepTableRecipeRegistry registry)
    {
        // Check salt storage
        if (GetBarrelSalt() > 0 && registry.MatchesCodeString("game:salt", code))
        {
            int current = GetBarrelSalt();
            SetBarrelSalt(System.Math.Max(0, current - quantity));
            Inventory[0].MarkDirty();
            MarkDirty(true);
            UpdateContentVisual();
            return;
        }

        // Check liquid content
        var barrelStack = Inventory[0].Itemstack;
        if (barrelStack == null) return;
        var container = barrelStack.Collectible as BlockLiquidContainerBase;
        if (container == null) return;
        var liquidContent = container.GetContent(barrelStack);
        if (liquidContent != null && registry.MatchesCode(liquidContent, code))
        {
            container.TryTakeContent(barrelStack, quantity);
            Inventory[0].MarkDirty();
            MarkDirty(true);
            UpdateContentVisual();
        }
    }

    protected override float[][] genTransformationMatrices()
    {
        var mat = new Matrixf()
            .Translate(0.5f, 4f / 16f, 0.5f)
            .Scale(0.5f, 0.5f, 0.5f)
            .Translate(-0.5f, 0f, -0.5f);
        return new[] { mat.Values };
    }
}

/// <summary>Right block entity — holds a tool (salt pan).</summary>
public class BlockEntityPrepTableRight : BlockEntityPrepTableSlot
{
    protected override string SlotClassName => "preptable-right";
    protected override string SlotLabel => Lang.Get("seafarer:preptable-slot-tool");
    protected override int AttachmentSelBox => 0; // Right: 0=tool slot, 1=body

    protected override bool IsValidItem(ItemStack stack) => true;

    public ItemStack? GetToolItem() => Inventory[0].Itemstack;

    protected override float[][] genTransformationMatrices()
    {
        // Center of right block, on table surface (Y=10px)
        var mat = new Matrixf()
            .Translate(0.5f, 10f / 16f, 0.5f)
            .Scale(0.45f, 0.45f, 0.45f)
            .Translate(-0.5f, 0f, -0.5f);
        return new[] { mat.Values };
    }
}
