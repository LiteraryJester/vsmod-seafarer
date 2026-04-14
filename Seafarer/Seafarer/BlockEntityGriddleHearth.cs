using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

public class BlockEntityGriddleHearth : BlockEntityDisplay
{
    public const int FoodSlotCount = 4;
    public const int FuelSlotIndex = 4;
    public const int TotalSlotCount = 5;
    public const int MaxFuelCount = 4;

    private bool burning;
    private float fuelBurnTimeLeft;
    private float hearthTemperature = 20f;
    private readonly float[] cookingProgress = new float[FoodSlotCount];
    private readonly GriddleRecipe?[] activeRecipes = new GriddleRecipe?[FoodSlotCount];
    private string griddleMaterial = "clay";
    private int rotationDeg;

    private InventoryGeneric? inventory;

    private GriddleConfig Config => SeafarerModSystem.GriddleConfig;

    public override InventoryBase Inventory => inventory!;
    public override string InventoryClassName => "griddlehearth";
    public override int DisplayedItems => TotalSlotCount;

    public ItemSlot FuelSlot => inventory![FuelSlotIndex];
    public bool IsBurning => burning;
    public bool HasFuel => !FuelSlot.Empty;
    public int FuelCount => FuelSlot.Itemstack?.StackSize ?? 0;
    public float Temperature => hearthTemperature;

    private const float BurnTimePerLog = 8f; // seconds per log
    private const float MaxTemp = 350f;
    private const float TempDecayPerSecond = 0.5f;

    public override void Initialize(ICoreAPI api)
    {
        inventory ??= new InventoryGeneric(TotalSlotCount, "griddlehearth-" + Pos, api);
        for (int i = 0; i < FoodSlotCount; i++)
        {
            inventory[i].MaxSlotStackSize = 1;
        }
        inventory[FuelSlotIndex].MaxSlotStackSize = MaxFuelCount;

        base.Initialize(api);

        if (api.Side == EnumAppSide.Server)
        {
            RegisterGameTickListener(OnHearthTick, Config.CookingTickIntervalMs);
        }

        SetRotation();
    }

    private void SetRotation()
    {
        switch (Block?.Variant["side"])
        {
            case "east": rotationDeg = 270; break;
            case "south": rotationDeg = 180; break;
            case "west": rotationDeg = 90; break;
            default: rotationDeg = 0; break;
        }
    }

    public bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

        if (activeSlot.Empty)
        {
            // Empty hand — try to remove food
            return TryTakeFood(byPlayer);
        }

        var heldItem = activeSlot.Itemstack;

        // Try to light first (before fuel check, so torch works even when fueled)
        if (IsFirestarter(heldItem))
        {
            return TryStartIgnite(byPlayer);
        }

        // Try to add fuel (only when no food on griddle)
        if (IsValidFuel(heldItem) && !HasFood())
        {
            return TryAddFuel(activeSlot, byPlayer);
        }

        // Try to add food (anytime — it cooks when there's heat)
        return TryPutFood(activeSlot, byPlayer);
    }

    private bool IsValidFuel(ItemStack stack)
    {
        if (stack == null) return false;
        string code = stack.Collectible.Code.Path;
        return code.StartsWith("firewood") || code.StartsWith("log-");
    }

    private bool IsFirestarter(ItemStack stack)
    {
        if (stack == null) return false;
        string code = stack.Collectible.Code.Path;
        return code.Contains("firestarter") || code.Contains("torch");
    }

    private bool TryAddFuel(ItemSlot slot, IPlayer byPlayer)
    {
        if (burning) return false;
        if (HasFood()) return false;

        if (FuelSlot.Empty || FuelSlot.StackSize < MaxFuelCount)
        {
            int moved = slot.TryPutInto(Api.World, FuelSlot);

            if (moved > 0)
            {
                updateMesh(FuelSlotIndex);
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/planks"), byPlayer.Entity, byPlayer, true, 16f);
                MarkDirty(true);
            }

            return moved > 0;
        }

        return false;
    }

    private const float IgniteTime = 3f;
    private bool igniting;

    private bool TryStartIgnite(IPlayer byPlayer)
    {
        if (burning || !HasFuel) return false;
        igniting = true;
        return true;
    }

    internal bool OnIgniteStep(float secondsUsed, IPlayer byPlayer)
    {
        if (!igniting) return false;

        if (Api.World.Rand.NextDouble() < 0.1)
            Api.World.PlaySoundAt(new AssetLocation("sounds/torch-ignite"), byPlayer.Entity, byPlayer, true, 16f);

        return secondsUsed < IgniteTime;
    }

    internal void OnIgniteStop(float secondsUsed, IPlayer byPlayer)
    {
        if (!igniting) return;
        igniting = false;

        if (secondsUsed < IgniteTime - 0.1f) return;
        if (Api.Side != EnumAppSide.Server) return;
        if (burning || !HasFuel) return;

        burning = true;
        fuelBurnTimeLeft = FuelCount * BurnTimePerLog;

        Api.World.PlaySoundAt(new AssetLocation("sounds/torch-ignite"), byPlayer.Entity, byPlayer, true, 16f);
        MarkDirty(true);
    }

    internal void OnIgniteCancel()
    {
        igniting = false;
    }

    private bool TryPutFood(ItemSlot slot, IPlayer byPlayer)
    {
        if (inventory == null) return false;

        var registry = Api.ModLoader.GetModSystem<GriddleRecipeRegistry>();
        if (registry == null) return false;

        var recipe = registry.GetMatchingRecipe(slot.Itemstack, null, griddleMaterial);
        if (recipe == null) return false;

        for (int i = 0; i < FoodSlotCount; i++)
        {
            if (inventory[i].Empty)
            {
                int moved = slot.TryPutInto(Api.World, inventory[i], 1);
                if (moved > 0)
                {
                    updateMesh(i);
                    Api.World.PlaySoundAt(new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16f);
                    MarkDirty(true);
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryTakeFood(IPlayer byPlayer)
    {
        if (inventory == null) return false;

        for (int i = FoodSlotCount - 1; i >= 0; i--)
        {
            if (!inventory[i].Empty)
            {
                ItemStack taken = inventory[i].TakeOutWhole();
                if (!byPlayer.InventoryManager.TryGiveItemstack(taken))
                {
                    Api.World.SpawnItemEntity(taken, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                updateMesh(i);
                cookingProgress[i] = 0;
                activeRecipes[i] = null;
                Api.World.PlaySoundAt(new AssetLocation("sounds/player/collect"), byPlayer.Entity, byPlayer, true, 16f);
                MarkDirty(true);
                return true;
            }
        }

        return false;
    }

    private bool HasFood()
    {
        if (inventory == null) return false;
        for (int i = 0; i < FoodSlotCount; i++)
        {
            if (!inventory[i].Empty) return true;
        }
        return false;
    }

    private void OnHearthTick(float dt)
    {
        if (Api.Side != EnumAppSide.Server) return;

        bool changed = false;

        if (burning)
        {
            fuelBurnTimeLeft -= dt;
            hearthTemperature = Math.Min(hearthTemperature + dt * 20f, MaxTemp);
            changed = true;

            if (fuelBurnTimeLeft <= 0)
            {
                fuelBurnTimeLeft = 0;
                burning = false;
                FuelSlot.Itemstack = null;
                FuelSlot.MarkDirty();
                changed = true;
            }
        }
        else if (hearthTemperature > 20f)
        {
            hearthTemperature = Math.Max(20f, hearthTemperature - TempDecayPerSecond * dt);
            changed = true;
        }

        if (inventory != null && hearthTemperature > 20f)
        {
            var registry = Api.ModLoader.GetModSystem<GriddleRecipeRegistry>();
            if (registry != null)
            {
                float speedMult = Config.GetCookSpeedMultiplier(griddleMaterial);

                for (int i = 0; i < FoodSlotCount; i++)
                {
                    ItemStack? inputStack = inventory[i]?.Itemstack;
                    if (inputStack == null)
                    {
                        if (cookingProgress[i] != 0)
                        {
                            cookingProgress[i] = 0;
                            activeRecipes[i] = null;
                        }
                        continue;
                    }

                    var recipe = registry.GetMatchingRecipe(inputStack, null, griddleMaterial);
                    if (recipe == null)
                    {
                        activeRecipes[i] = null;
                        continue;
                    }

                    activeRecipes[i] = recipe;

                    if (hearthTemperature < recipe.CookingTemp) continue;

                    float tickSeconds = Config.CookingTickIntervalMs / 1000f;
                    cookingProgress[i] += tickSeconds * speedMult;

                    if (cookingProgress[i] >= recipe.CookingDuration)
                    {
                        CompleteCooking(i, recipe, inputStack, registry);
                    }

                    changed = true;
                }
            }
        }

        if (changed)
        {
            MarkDirty(false);
        }
    }

    private void CompleteCooking(int slotIndex, GriddleRecipe recipe, ItemStack inputStack, GriddleRecipeRegistry registry)
    {
        string outputCode = registry.ResolveOutputCode(recipe, inputStack);

        CollectibleObject? outputCollectible = recipe.Output.Type == "block"
            ? Api.World.GetBlock(new AssetLocation(outputCode))
            : Api.World.GetItem(new AssetLocation(outputCode));

        if (outputCollectible != null && inventory != null)
        {
            var outputStack = new ItemStack(outputCollectible, recipe.Output.Quantity);

            var contents = inputStack.Attributes?.GetTreeAttribute("contents");
            if (contents != null)
            {
                outputStack.Attributes["contents"] = contents.Clone();
            }

            inventory[slotIndex].Itemstack = outputStack;
            inventory[slotIndex].MarkDirty();
            updateMesh(slotIndex);
        }

        cookingProgress[slotIndex] = 0;
        activeRecipes[slotIndex] = null;

        MarkDirty(true);
    }

    public void DropContents(IWorldAccessor world, BlockPos pos)
    {
        if (inventory == null) return;

        for (int i = 0; i < FoodSlotCount; i++)
        {
            if (!inventory[i].Empty)
            {
                world.SpawnItemEntity(inventory[i].TakeOutWhole(), pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        // Drop fuel as items
        if (!FuelSlot.Empty)
        {
            world.SpawnItemEntity(FuelSlot.TakeOutWhole(), pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }

        // Drop the griddle if it's metal
        if (griddleMaterial != "clay")
        {
            var griddleBlock = Api.World.GetBlock(new AssetLocation("seafarer:claygriddle-" + griddleMaterial));
            if (griddleBlock != null)
            {
                world.SpawnItemEntity(new ItemStack(griddleBlock), pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }
    }

    public void SetGriddleMaterial(string material)
    {
        griddleMaterial = material;
        MarkDirty(true);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);

        if (burning)
        {
            sb.AppendLine(Lang.Get("seafarer:hearth-burning", (int)hearthTemperature));
        }
        else if (hearthTemperature > 30f)
        {
            sb.AppendLine(Lang.Get("seafarer:hearth-heated", (int)hearthTemperature));
        }
        else if (HasFuel)
        {
            sb.AppendLine(Lang.Get("seafarer:hearth-fueled", FuelCount));
        }
        else
        {
            sb.AppendLine(Lang.Get("seafarer:hearth-cold"));
        }

        for (int i = 0; i < FoodSlotCount; i++)
        {
            var stack = inventory?[i]?.Itemstack;
            if (stack == null) continue;

            var recipe = activeRecipes[i];
            if (recipe != null && cookingProgress[i] > 0)
            {
                float pct = Math.Clamp((cookingProgress[i] / recipe.CookingDuration) * 100f, 0f, 100f);
                sb.AppendLine(Lang.Get("seafarer:griddle-cooking", stack.GetName(), (int)pct));
            }
            else
            {
                sb.AppendLine(stack.GetName());
            }
        }
    }

    #region Meshing

    protected override string getMeshCacheKey(ItemSlot slot)
    {
        if (slot == FuelSlot)
        {
            return slot.StackSize + "x-hearthfuel";
        }
        return base.getMeshCacheKey(slot);
    }

    protected override MeshData getOrCreateMesh(ItemSlot slot, int index)
    {
        if (index == FuelSlotIndex)
        {
            MeshData mesh = getMesh(slot);
            if (mesh != null) return mesh;

            var stack = slot.Itemstack;
            if (stack == null) return null!;

            string shapeLoc = Block.Attributes?["hearthFuelShape"].AsString()
                ?? "seafarer:block/clay/griddlehearth-fuel";

            var loc = AssetLocation.Create(shapeLoc, Block.Code.Domain)
                .WithPathPrefixOnce("shapes/")
                .WithPathAppendixOnce(".json");

            nowTesselatingShape = Shape.TryGet(capi, loc);
            nowTesselatingObj = stack.Collectible;

            if (nowTesselatingShape == null) return null!;

            capi.Tesselator.TesselateShape(
                "hearthFuelShape", nowTesselatingShape, out mesh, this,
                null, 0, 0, 0, stack.StackSize);

            string key = getMeshCacheKey(slot);
            MeshCache[key] = mesh;

            return mesh;
        }

        return base.getOrCreateMesh(slot, index);
    }

    protected override float[][] genTransformationMatrices()
    {
        float[][] matrices = new float[TotalSlotCount][];

        // Griddle surface is at Y=9/16 in the combined shape
        // Arrange 4 food items in quadrants on that surface
        float[][] offsets =
        [
            [0.05f, 0.05f],
            [0.05f, 0.5f],
            [0.5f, 0.05f],
            [0.5f, 0.5f],
        ];

        for (int i = 0; i < FoodSlotCount; i++)
        {
            var mat = Matrixf.Create();
            mat.Translate(offsets[i][0], 9f / 16f, offsets[i][1]);
            mat.Scale(0.5f, 0.5f, 0.5f);
            matrices[i] = mat.Values;
        }

        // Fuel slot: identity transform positioned at origin, rotated to match block facing
        var fuelMat = new Matrixf()
            .Translate(0.5f, 0f, 0.5f)
            .RotateYDeg(rotationDeg)
            .Translate(-0.5f, 0f, -0.5f);
        matrices[FuelSlotIndex] = fuelMat.Values;

        return matrices;
    }

    #endregion

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        inventory?.ToTreeAttributes(tree.GetOrAddTreeAttribute("inventory"));

        tree.SetBool("burning", burning);
        tree.SetFloat("fuelBurnTimeLeft", fuelBurnTimeLeft);
        tree.SetFloat("hearthTemperature", hearthTemperature);
        tree.SetString("griddleMaterial", griddleMaterial);

        for (int i = 0; i < FoodSlotCount; i++)
        {
            tree.SetFloat("cookProgress" + i, cookingProgress[i]);
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        if (inventory == null)
        {
            inventory = new InventoryGeneric(TotalSlotCount, "griddlehearth-" + Pos, worldForResolving.Api);
            for (int i = 0; i < FoodSlotCount; i++)
            {
                inventory[i].MaxSlotStackSize = 1;
            }
            inventory[FuelSlotIndex].MaxSlotStackSize = MaxFuelCount;
        }

        base.FromTreeAttributes(tree, worldForResolving);
        inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));

        burning = tree.GetBool("burning", false);
        fuelBurnTimeLeft = tree.GetFloat("fuelBurnTimeLeft", 0);
        hearthTemperature = tree.GetFloat("hearthTemperature", 20f);
        griddleMaterial = tree.GetString("griddleMaterial", "clay");

        for (int i = 0; i < FoodSlotCount; i++)
        {
            cookingProgress[i] = tree.GetFloat("cookProgress" + i, 0f);
        }

        RedrawAfterReceivingTreeAttributes(worldForResolving);
    }
}
