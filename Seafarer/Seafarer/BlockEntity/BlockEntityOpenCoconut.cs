using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

/// <summary>
/// Block entity for the open coconut liquid container.
/// On first creation (not loaded from save), auto-fills with 1L of coconut water.
/// </summary>
public class BlockEntityOpenCoconut : BlockEntityLiquidContainer
{
    public override string InventoryClassName => "opencoconut";

    private MeshData? currentMesh;

    public BlockEntityOpenCoconut()
    {
        inventory = new InventoryGeneric(1, null, null);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (api.Side == EnumAppSide.Client)
        {
            currentMesh = GenMesh();
            MarkDirty(true);
        }
    }

    public override void OnBlockPlaced(ItemStack? byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);

        // Auto-fill with coconut water only when placed programmatically
        // (BehaviorCoconutCrack calls SetBlock with no itemstack).
        // When placed from inventory, the itemstack already carries its liquid
        // contents (set by the prep table or serialized from a prior placement).
        if (byItemStack == null && GetContent() == null)
        {
            FillWithCoconutWater();
        }

        if (Api?.Side == EnumAppSide.Client)
        {
            currentMesh = GenMesh();
            MarkDirty(true);
        }
    }

    private void FillWithCoconutWater()
    {
        Item? waterItem = Api.World.GetItem(new AssetLocation("seafarer:coconutwaterportion"));
        if (waterItem == null) return;

        // 100 items per litre × 1 litre = 100
        var waterStack = new ItemStack(waterItem, 100);
        SetContent(waterStack);
    }

    private MeshData? GenMesh()
    {
        if (Block is not BlockLiquidContainerTopOpened containerBlock) return null;
        return containerBlock.GenMesh(Api as ICoreClientAPI, GetContent(), Pos);
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        if (currentMesh != null)
        {
            mesher.AddMeshData(currentMesh);
        }
        return true;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);

        if (Api?.Side == EnumAppSide.Client)
        {
            currentMesh = GenMesh();
            MarkDirty(true);
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        ItemSlot slot = inventory[0];
        if (slot.Empty)
        {
            dsc.AppendLine(Lang.Get("Empty"));
        }
        else
        {
            dsc.AppendLine(Lang.Get("Contents: {0}x{1}", slot.Itemstack.StackSize, slot.Itemstack.GetName()));
        }
    }
}
