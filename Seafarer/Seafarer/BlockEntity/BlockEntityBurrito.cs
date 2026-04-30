using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Seafarer;

public class BlockEntityBurrito : BlockEntity
{
    public const int MaxFillings = 2;

    private readonly ItemStack?[] fillings = new ItemStack?[MaxFillings];

    public int FillingCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < MaxFillings; i++)
                if (fillings[i] != null) count++;
            return count;
        }
    }

    public bool OnInteract(IPlayer byPlayer)
    {
        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

        if (activeSlot.Empty)
        {
            return TryPickup(byPlayer);
        }

        return TryAddFilling(activeSlot, byPlayer);
    }

    private bool TryPickup(IPlayer byPlayer)
    {
        var stack = CreateBurritoStack();
        if (stack == null) return false;

        if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
        {
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.25, 0.5));
        }

        Api.World.BlockAccessor.SetBlock(0, Pos);
        return true;
    }

    private bool TryAddFilling(ItemSlot slot, IPlayer byPlayer)
    {
        if (Block.Variant["state"] != "raw") return false;

        if (FillingCount >= MaxFillings)
        {
            if (Api is ICoreClientAPI capi)
                capi.TriggerIngameError(this, "burritofull", Lang.Get("seafarer:burrito-full"));
            return false;
        }

        var item = slot.Itemstack;
        if (item == null) return false;
        var props = item.ItemAttributes?["inBurritoProperties"];
        if (props == null || !props.Exists)
        {
            if (Api is ICoreClientAPI capi)
                capi.TriggerIngameError(this, "notburritofilling", Lang.Get("seafarer:burrito-invalid-filling"));
            return false;
        }

        string partType = props["partType"].AsString("");
        if (partType != "Filling") return false;

        for (int i = 0; i < MaxFillings; i++)
        {
            if (fillings[i] == null)
            {
                fillings[i] = slot.TakeOut(1);
                slot.MarkDirty();
                MarkDirty(true);
                Api.World.PlaySoundAt(new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16f);
                return true;
            }
        }

        return false;
    }

    public ItemStack? CreateBurritoStack()
    {
        var stack = new ItemStack(Block);
        SaveContents(stack);
        return stack;
    }

    private void SaveContents(ItemStack stack)
    {
        var tree = stack.Attributes.GetOrAddTreeAttribute("contents");
        for (int i = 0; i < MaxFillings; i++)
        {
            if (fillings[i] != null)
                tree["slot" + i] = new ItemstackAttribute(fillings[i]);
        }
    }

    public void LoadFromItemStack(ItemStack stack)
    {
        var tree = stack.Attributes?.GetTreeAttribute("contents");
        if (tree == null) return;

        for (int i = 0; i < MaxFillings; i++)
        {
            fillings[i] = (tree["slot" + i] as ItemstackAttribute)?.value;
            fillings[i]?.ResolveBlockOrItem(Api.World);
        }

        MarkDirty(true);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);

        bool hasFillings = false;
        for (int i = 0; i < MaxFillings; i++)
        {
            if (fillings[i] == null) continue;
            if (!hasFillings)
            {
                sb.AppendLine(Lang.Get("seafarer:burrito-fillings"));
                hasFillings = true;
            }

            sb.AppendLine("  " + fillings[i]!.GetName());
        }

        if (!hasFillings && Block.Variant["state"] == "raw")
        {
            sb.AppendLine(Lang.Get("seafarer:burrito-empty"));
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        var contents = tree.GetOrAddTreeAttribute("contents");
        for (int i = 0; i < MaxFillings; i++)
        {
            if (fillings[i] != null)
                contents["slot" + i] = new ItemstackAttribute(fillings[i]);
            else
                contents.RemoveAttribute("slot" + i);
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        var contents = tree.GetTreeAttribute("contents");
        if (contents == null) return;

        for (int i = 0; i < MaxFillings; i++)
        {
            fillings[i] = (contents["slot" + i] as ItemstackAttribute)?.value;
            fillings[i]?.ResolveBlockOrItem(worldForResolving);
        }
    }
}
