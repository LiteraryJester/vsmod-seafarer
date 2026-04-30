using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Seafarer;

public class BlockEntityAmphoraStorage : BlockEntityGenericTypedContainer
{
    public bool Sealed { get; set; }

    public bool HasContents()
    {
        if (Inventory == null) return false;
        for (int i = 0; i < Inventory.Count; i++)
        {
            if (!Inventory[i].Empty) return true;
        }
        return false;
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        if (Inventory != null)
        {
            Inventory.OnAcquireTransitionSpeed += OnAcquireTransitionSpeed;
        }
    }

    private float OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float mulByConfig)
    {
        if (transType == EnumTransitionType.Perish && Sealed)
        {
            return 0.1f;
        }
        return 1f;
    }

    public override void OnBlockPlaced(ItemStack? byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);
        if (byItemStack != null)
        {
            Sealed = byItemStack.Attributes.GetBool("sealed");
        }
    }

    public override void OnBlockBroken(IPlayer? byPlayer = null)
    {
        // Save sealed state to the dropped itemstack
        if (Sealed && Api.Side == EnumAppSide.Server)
        {
            // The base class handles dropping the block; we intercept via ToTreeAttributes
            // which is read when creating the itemstack drop
        }
        base.OnBlockBroken(byPlayer);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetBool("sealed", Sealed);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        Sealed = tree.GetBool("sealed");
    }
}
