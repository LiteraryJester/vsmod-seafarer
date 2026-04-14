using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

public class BlockEntityDryingFrame : BlockEntityDisplay
{
    private InventoryGeneric inventory;
    private float currentRainfall;
    private double currentWindSpeed;
    private bool exposedToSky = true;

    public override InventoryBase Inventory => inventory;
    public override string InventoryClassName => "dryingframe";
    public override string AttributeTransformCode => "onDryingFrameProps";

    private DryingFrameConfig Config => SeafarerModSystem.Config;

    public BlockEntityDryingFrame()
    {
        inventory = new InventoryDisplayed(this, 4, "dryingframe-0", null);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        Inventory.OnAcquireTransitionSpeed += OnTransitionSpeed;

        if (api.Side == EnumAppSide.Server && (Config.EnableRainRot || Config.EnableWindDrying))
        {
            RegisterGameTickListener(UpdateWeather, Config.WeatherCheckIntervalMs);
        }
    }

    private void UpdateWeather(float dt)
    {
        var ba = Api.World.BlockAccessor;
        int rainMapHeight = ba.GetRainMapHeightAt(Pos);
        exposedToSky = Pos.Y >= rainMapHeight;

        if (exposedToSky)
        {
            if (Config.EnableRainRot)
            {
                var climate = ba.GetClimateAt(Pos, EnumGetClimateMode.NowValues, 0);
                currentRainfall = climate?.Rainfall ?? 0f;
            }
            else
            {
                currentRainfall = 0f;
            }

            if (Config.EnableWindDrying)
            {
                var windVec = ba.GetWindSpeedAt(Pos);
                currentWindSpeed = windVec.Length();
            }
            else
            {
                currentWindSpeed = 0;
            }
        }
        else
        {
            currentRainfall = 0f;
            currentWindSpeed = 0;
        }

        MarkDirty(false);
    }

    private float GetDrySpeedMultiplier()
    {
        float mul = Config.DryingSpeedMultiplier;
        if (Config.EnableWindDrying && currentWindSpeed > 0)
        {
            mul += (float)currentWindSpeed * Config.WindDryMultiplier;
        }
        return mul;
    }

    private float OnTransitionSpeed(EnumTransitionType transType, ItemStack stack, float mulByConfig)
    {
        if (Api == null) return 1f;

        if (transType == EnumTransitionType.Perish && Config.EnableRainRot && currentRainfall > Config.RainThreshold)
        {
            return 1f + (currentRainfall * (Config.RainRotMultiplier - 1f));
        }

        if (transType == EnumTransitionType.Dry)
        {
            // mulByConfig includes room-based modifiers (e.g. 0.25x for sealed rooms).
            // The rack's drying speed should be solely determined by config + wind,
            // so divide out the room modifier to replace it with our intended value.
            return mulByConfig > 0 ? GetDrySpeedMultiplier() / mulByConfig : GetDrySpeedMultiplier();
        }

        return 1f;
    }

    internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

        if (activeSlot.Empty)
        {
            return TryTake(byPlayer, blockSel);
        }

        if (TryPut(activeSlot, blockSel))
        {
            Api.World.PlaySoundAt(
                new AssetLocation("sounds/player/build"),
                byPlayer.Entity, byPlayer, true, 16f);
            return true;
        }

        return false;
    }

    private bool TryPut(ItemSlot slot, BlockSelection blockSel)
    {
        int index = blockSel.SelectionBoxIndex;
        if (index < 0 || index >= 4) return false;

        if (slot.Itemstack?.Collectible?.Attributes?["onDryingFrameProps"]?.Exists != true)
        {
            return false;
        }

        if (Inventory[index].Empty && slot.TryPutInto(Api.World, Inventory[index], 1) > 0)
        {
            updateMesh(index);
            MarkDirty(true);
            return true;
        }
        return false;
    }

    private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
    {
        int index = blockSel.SelectionBoxIndex;
        if (index < 0 || index >= 4) return false;

        if (!Inventory[index].Empty)
        {
            ItemStack stack = Inventory[index].TakeOut(1);
            if (byPlayer.InventoryManager.TryGiveItemstack(stack))
            {
                Api.World.PlaySoundAt(
                    new AssetLocation("sounds/player/build"),
                    byPlayer.Entity, byPlayer, true, 16f);
            }
            if (stack.StackSize > 0)
            {
                Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
            updateMesh(index);
            MarkDirty(true);
            return true;
        }
        return false;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        sb.AppendLine();
        if (forPlayer?.CurrentBlockSelection == null) return;

        int index = forPlayer.CurrentBlockSelection.SelectionBoxIndex;
        if (index < 0 || index >= 4) return;

        if (!Inventory[index].Empty)
        {
            ItemStack? stack = Inventory[index].Itemstack;
            sb.AppendLine(stack?.GetName());

            if (stack != null)
            {
                TransitionState[] states = stack.Collectible.UpdateAndGetTransitionStates(Api.World, Inventory[index]);
                if (states != null)
                {
                    foreach (var state in states)
                    {
                        if (state.Props.Type == EnumTransitionType.Dry)
                        {
                            float hoursLeft;
                            if (state.FreshHoursLeft > 0)
                            {
                                hoursLeft = state.FreshHoursLeft + state.TransitionHours;
                            }
                            else
                            {
                                hoursLeft = state.TransitionHours * (1f - state.TransitionLevel);
                            }

                            float effectiveHours = hoursLeft / GetDrySpeedMultiplier();
                            float days = effectiveHours / 24f;
                            sb.AppendLine(Lang.Get("seafarer:dryingframe-time-remaining", days));
                            break;
                        }
                    }
                }
            }
        }

        if (!exposedToSky)
        {
            sb.AppendLine("<font color=\"#ff4444\">" + Lang.Get("seafarer:dryingframe-not-exposed") + "</font>");
        }
        else if (Config.EnableRainRot && currentRainfall > Config.RainThreshold)
        {
            sb.AppendLine(Lang.Get("seafarer:dryingframe-rain-warning"));
        }

        if (Config.EnableWindDrying && currentWindSpeed > 0.01)
        {
            float windBonus = (float)currentWindSpeed * Config.WindDryMultiplier;
            float speedup = (Config.DryingSpeedMultiplier + windBonus) / Config.DryingSpeedMultiplier;
            sb.AppendLine(Lang.Get("seafarer:dryingframe-wind-bonus", speedup));
        }
    }

    protected override float[][] genTransformationMatrices()
    {
        float[][] matrices = new float[4][];

        // Rope1: y=26-27px, z=7.3-8.3px. Items hang down from it, spaced along X.
        float ropeY = 25f / 16f;    // top of rope in block coords
        float ropeZ = 7.8f / 16f;   // rope center Z in block coords
        float[] itemX = { 0.425f, 0.55f, 0.725f, 0.85f }; // 4 positions along X

        string side = Block.Variant["side"];
        bool eastWest = side == "east" || side == "west";

        for (int i = 0; i < 4; i++)
        {
            Matrixf mat;

            if (eastWest)
            {
                mat = new Matrixf()
                    .Translate(ropeZ, ropeY, itemX[i])
                    .RotateXDeg(180f)
                    .RotateZDeg(90f)
                    .RotateXDeg(90f);
            }
            else
            {
                mat = new Matrixf()
                    .Translate(itemX[i], ropeY, ropeZ)
                    .RotateXDeg(180f)
                    .RotateZDeg(90f);
            }

            matrices[i] = mat
                .Scale(0.75f, 1f, 0.75f)
                .Translate(-0.5f, 0f, -0.5f)
                .Values;
        }
        return matrices;
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetFloat("currentRainfall", currentRainfall);
        tree.SetDouble("currentWindSpeed", currentWindSpeed);
        tree.SetBool("exposedToSky", exposedToSky);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        currentRainfall = tree.GetFloat("currentRainfall", 0f);
        currentWindSpeed = tree.GetDouble("currentWindSpeed", 0);
        exposedToSky = tree.GetBool("exposedToSky", true);

        // Rebuild display meshes on client when inventory changes arrive
        if (Api?.Side == EnumAppSide.Client)
        {
            for (int i = 0; i < 4; i++)
            {
                updateMesh(i);
            }
        }
    }
}
