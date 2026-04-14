using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

public class BlockEntitySaltPan : BlockEntity
{
    private float currentLitres;
    private bool evaporating;
    private float currentRainfall;
    private bool exposedToSky = true;
    private double lastTickTotalHours;
    private int saltAmount;

    private SaltPanConfig Config => SeafarerModSystem.SaltPanConfig;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (api.Side == EnumAppSide.Server)
        {
            lastTickTotalHours = api.World.Calendar.TotalHours;
            RegisterGameTickListener(OnEvaporationTick, Config.WeatherCheckIntervalMs);

            // Backfill saltAmount for pans that completed evaporation before this field existed
            if (Block.Variant["contents"] == "salt" && saltAmount <= 0)
            {
                saltAmount = (int)Math.Max(1, Config.CapacityLitres * Config.SaltYieldPerLitre);
                MarkDirty(false);
            }
        }
    }

    private void OnEvaporationTick(float dt)
    {
        var ba = Api.World.BlockAccessor;
        int rainMapHeight = ba.GetRainMapHeightAt(Pos);
        exposedToSky = Pos.Y >= rainMapHeight;

        var climate = ba.GetClimateAt(Pos, EnumGetClimateMode.NowValues, 0);
        float temperature = climate?.Temperature ?? 0f;
        currentRainfall = (exposedToSky ? (climate?.Rainfall ?? 0f) : 0f);

        if (!evaporating || !Config.Enabled)
        {
            lastTickTotalHours = Api.World.Calendar.TotalHours;
            MarkDirty(false);
            return;
        }

        // Stall conditions
        if (!exposedToSky || currentRainfall > Config.RainThreshold || temperature <= Config.MinEvaporationTemperature)
        {
            lastTickTotalHours = Api.World.Calendar.TotalHours;
            MarkDirty(false);
            return;
        }

        double totalHours = Api.World.Calendar.TotalHours;
        float gameHoursElapsed = (float)(totalHours - lastTickTotalHours);
        lastTickTotalHours = totalHours;

        if (gameHoursElapsed <= 0f) return;

        float tempMultiplier = Math.Clamp(temperature / Config.TemperatureScaleBase, 0f, Config.MaxTemperatureMultiplier);
        float effectiveRate = Config.BaseEvapRatePerHour * tempMultiplier;
        float litresEvaporated = effectiveRate * gameHoursElapsed;

        currentLitres -= litresEvaporated;
        if (currentLitres <= 0f)
        {
            currentLitres = 0f;
            evaporating = false;
            saltAmount = (int)Math.Max(1, Config.CapacityLitres * Config.SaltYieldPerLitre);
            SetContentsState("salt");
            MarkDirty(true);
            return;
        }

        MarkDirty(false);
    }

    private void SetContentsState(string state)
    {
        AssetLocation loc = Block.CodeWithVariant("contents", state);
        Block newBlock = Api.World.GetBlock(loc)!;
        if (newBlock != null)
        {
            Api.World.BlockAccessor.ExchangeBlock(newBlock.Id, Pos);
            this.Block = newBlock;
        }
    }

    internal bool TryFill(IPlayer byPlayer)
    {
        if (evaporating) return false;

        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot.Empty) return false;

        var container = activeSlot.Itemstack?.Collectible as BlockLiquidContainerBase;
        if (container == null) return false;

        ItemStack contentStack = container.GetContent(activeSlot.Itemstack!)!;
        if (contentStack == null) return false;

        if (contentStack.Collectible.Code.Path != "saltwaterportion") return false;

        float remainingCapacity = Config.CapacityLitres - currentLitres;
        if (remainingCapacity <= 0f) return false;

        var props = BlockLiquidContainerBase.GetContainableProps(contentStack);
        if (props == null) return false;

        // Only transfer one container's worth
        float singleCapacity = activeSlot.Itemstack?.Collectible?.Attributes?["capacityLitres"]?.AsFloat(10f) ?? 10f;
        float oneLitres = Math.Min(contentStack.StackSize / props.ItemsPerLitre, singleCapacity);
        float transferAmount = Math.Min(remainingCapacity, oneLitres);
        int itemsToRemove = (int)Math.Ceiling(transferAmount * props.ItemsPerLitre);
        if (itemsToRemove <= 0) return false;

        if (Api.Side != EnumAppSide.Server) return true;

        // Use SplitStackAndPerformAction to handle stacked containers
        container.SplitStackAndPerformAction(byPlayer.Entity, activeSlot, (stack) =>
        {
            container.TryTakeContent(stack, itemsToRemove);
            return itemsToRemove;
        });

        currentLitres += transferAmount;
        if (currentLitres >= Config.CapacityLitres)
        {
            currentLitres = Config.CapacityLitres;
            evaporating = true;
            SetContentsState("water");
        }

        MarkDirty(true);
        return true;
    }

    internal bool CanHarvest()
    {
        return Block.Variant["contents"] == "salt" && saltAmount > 0;
    }

    internal bool TryHarvest(IPlayer byPlayer)
    {
        if (!CanHarvest()) return false;

        var saltItem = Api.World.GetItem(new AssetLocation("game:salt"));
        if (saltItem == null) return false;
        ItemStack saltStack = new ItemStack(saltItem, saltAmount);

        if (!byPlayer.InventoryManager.TryGiveItemstack(saltStack))
        {
            Api.World.SpawnItemEntity(saltStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }

        saltAmount = 0;
        SetContentsState("empty");
        MarkDirty(true);
        return true;
    }

    internal bool TrySaltMeat(IPlayer byPlayer)
    {
        if (Block.Variant["contents"] != "salt") return false;
        if (saltAmount <= 0) return false;

        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot.Empty) return false;

        // Check for saltRubbedOutput attribute on the held item
        string? saltedCode = activeSlot.Itemstack.Collectible.Attributes?["saltRubbedOutput"]?.AsString();
        if (saltedCode == null) return false;

        var saltedItem = Api.World.GetItem(new AssetLocation(saltedCode));
        if (saltedItem == null) return false;

        activeSlot.TakeOut(1);
        activeSlot.MarkDirty();

        ItemStack saltedStack = new ItemStack(saltedItem, 1);
        if (!byPlayer.InventoryManager.TryGiveItemstack(saltedStack))
        {
            Api.World.SpawnItemEntity(saltedStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }

        saltAmount--;
        if (saltAmount <= 0)
        {
            saltAmount = 0;
            SetContentsState("empty");
        }

        MarkDirty(true);
        return true;
    }

    internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
    {
        // Harvest is handled by BlockSaltPan via Start/Step/Stop pattern
        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot.Empty) return false;

        if (TrySaltMeat(byPlayer))
        {
            Api.World.PlaySoundAt(new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16f);
            return true;
        }

        if (TryFill(byPlayer))
        {
            Api.World.PlaySoundAt(new AssetLocation("sounds/environment/waterfill"), byPlayer.Entity, byPlayer, true, 16f);
            return true;
        }

        return false;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);

        string contents = Block.Variant["contents"];

        if (contents == "salt")
        {
            sb.AppendLine(Lang.Get("seafarer:saltpan-ready"));
            sb.AppendLine(Lang.Get("seafarer:saltpan-salt-remaining", saltAmount));
        }
        else if (evaporating)
        {
            float progress = 1f - (currentLitres / Config.CapacityLitres);
            float percent = Math.Clamp(progress * 100f, 0f, 100f);

            if (!exposedToSky || currentRainfall > Config.RainThreshold)
            {
                sb.AppendLine(Lang.Get("seafarer:saltpan-evaporating-stalled", percent));
            }
            else
            {
                sb.AppendLine(Lang.Get("seafarer:saltpan-evaporating", percent));
            }
        }
        else if (currentLitres > 0f)
        {
            sb.AppendLine(Lang.Get("seafarer:saltpan-filling", currentLitres, Config.CapacityLitres));
        }
        else
        {
            sb.AppendLine(Lang.Get("seafarer:saltpan-empty"));
        }

        if (!exposedToSky)
        {
            sb.AppendLine("<font color=\"#ff4444\">" + Lang.Get("seafarer:saltpan-no-sky") + "</font>");
        }
        else if (evaporating && currentRainfall > Config.RainThreshold)
        {
            sb.AppendLine(Lang.Get("seafarer:saltpan-raining"));
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetFloat("currentLitres", currentLitres);
        tree.SetBool("evaporating", evaporating);
        tree.SetFloat("currentRainfall", currentRainfall);
        tree.SetBool("exposedToSky", exposedToSky);
        tree.SetInt("saltAmount", saltAmount);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        currentLitres = tree.GetFloat("currentLitres", 0f);
        evaporating = tree.GetBool("evaporating", false);
        currentRainfall = tree.GetFloat("currentRainfall", 0f);
        exposedToSky = tree.GetBool("exposedToSky", true);
        saltAmount = tree.GetInt("saltAmount", 0);
    }
}
