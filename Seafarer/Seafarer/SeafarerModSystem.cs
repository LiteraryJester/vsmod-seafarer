using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Seafarer
{
    public class SeafarerModSystem : ModSystem
    {
        public const string ConfigFilename = "SaltAndSandConfig.json";
        private const string HarmonyId = "seafarer";

        public static DryingFrameConfig Config { get; private set; } = new();
        public static SaltPanConfig SaltPanConfig { get; private set; } = new();
        public static GriddleConfig GriddleConfig { get; private set; } = new();
        public static ExposureConfig ExposureConfig { get; private set; } = new();
        public static MudRakeConfig MudRakeConfig { get; private set; } = new();

        private Harmony? harmony;

        public override void Start(ICoreAPI api)
        {
            Mod.Logger.Notification("Hello from template mod: " + api.Side);
            api.RegisterBlockClass("BlockDryingFrame", typeof(BlockDryingFrame));
            api.RegisterBlockEntityClass("BlockEntityDryingFrame", typeof(BlockEntityDryingFrame));
            api.RegisterBlockClass("BlockSaltPan", typeof(BlockSaltPan));
            api.RegisterBlockEntityClass("BlockEntitySaltPan", typeof(BlockEntitySaltPan));
            api.RegisterBlockClass("BlockAmphora", typeof(BlockAmphora));
api.RegisterBlockClass("BlockAmphoraStorage", typeof(BlockAmphoraStorage));
            api.RegisterBlockEntityClass("BlockEntityAmphoraStorage", typeof(BlockEntityAmphoraStorage));
            api.RegisterBlockClass("BlockGriddle", typeof(BlockGriddle));
            api.RegisterBlockClass("BlockGriddleHearthBase", typeof(BlockGriddleHearthBase));
            api.RegisterBlockClass("BlockGriddleHearth", typeof(BlockGriddleHearth));
            api.RegisterBlockEntityClass("GriddleHearthEntity", typeof(BlockEntityGriddleHearth));
            api.RegisterBlockClass("BlockPrepTable", typeof(BlockPrepTable));
            api.RegisterBlockEntityClass("BlockEntityPrepTable", typeof(BlockEntityPrepTable));
            api.RegisterBlockEntityClass("BlockEntityPrepTablePlatform", typeof(BlockEntityPrepTablePlatform));
            api.RegisterBlockEntityClass("BlockEntityPrepTableRight", typeof(BlockEntityPrepTableRight));
            api.RegisterBlockClass("BlockOpenCoconut", typeof(BlockOpenCoconut));
            api.RegisterBlockEntityClass("BlockEntityOpenCoconut", typeof(BlockEntityOpenCoconut));
            api.RegisterBlockClass("BlockBurrito", typeof(BlockBurrito));
            api.RegisterBlockEntityClass("BlockEntityBurrito", typeof(BlockEntityBurrito));
            api.RegisterBlockClass("BlockGrowPot", typeof(BlockGrowPot));
            api.RegisterCollectibleBehaviorClass("PlaceBurrito", typeof(BehaviorPlaceBurrito));
            api.RegisterCollectibleBehaviorClass("CoconutCrack", typeof(BehaviorCoconutCrack));
            api.RegisterCollectibleBehaviorClass("boatrepair", typeof(BehaviorBoatRepair));
            api.RegisterCollectibleBehaviorClass("boatsail", typeof(BehaviorBoatSail));
            api.RegisterItemClass("ItemMudRake", typeof(ItemMudRake));
            api.RegisterCollectibleBehaviorClass("ClamShuck", typeof(BehaviorClamShuck));
            api.RegisterCollectibleBehaviorClass("ShellCrush", typeof(BehaviorShellCrush));
            api.RegisterEntityBehaviorClass("seafarer:exposure", typeof(EntityBehaviorExposure));
            api.RegisterEntityBehaviorClass("shipmechanics", typeof(EntityBehaviorShipMechanics));
            api.RegisterEntity("EntityProjectileBarbed", typeof(EntityProjectileBarbed));
            api.RegisterItemClass("ItemOceanLocatorMap", typeof(ItemOceanLocatorMap));
            api.RegisterEntity("EntityOutriggerBoat", typeof(EntityOutriggerBoat));
            api.RegisterItemClass("ItemOutriggerBoat", typeof(ItemOutriggerBoat));
            api.RegisterItemClass("ItemOutriggerRollers", typeof(ItemOutriggerRollers));

            harmony = new Harmony(HarmonyId);
            harmony.PatchAll(typeof(SeafarerModSystem).Assembly);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            base.Dispose();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);
            BoatTraitRegistry.Load(api);
            if (api is ICoreServerAPI sapi)
            {
                LoadConfigs(sapi, log: true);
                if (sapi.ModLoader.IsModEnabled("configlib"))
                {
                    HookConfigLib(sapi);
                }
            }
        }

        // Gated by IsModEnabled("configlib") — ConfigLib types are only resolved
        // when this method is JITted, so the mod still loads if ConfigLib is absent.
        private void HookConfigLib(ICoreServerAPI api)
        {
            var provider = api.ModLoader.GetModSystem<ConfigLib.ConfigLibModSystem>();
            if (provider == null) return;
            provider.SettingChanged += (domain, _, _) =>
            {
                if (domain == "seafarer") LoadConfigs(api, log: false);
            };
        }

        private void LoadConfigs(ICoreServerAPI api, bool log = true)
        {
            var defaults = LoadAssetDefaults(api);

            Config = new DryingFrameConfig();
            if (defaults != null) ApplyDryingFrameDefaults(Config, defaults);

            GriddleConfig = new GriddleConfig();
            if (defaults != null) ApplyGriddleDefaults(GriddleConfig, defaults);

            SaltPanConfig = new SaltPanConfig();
            if (defaults != null) ApplySaltPanDefaults(SaltPanConfig, defaults);

            ExposureConfig = new ExposureConfig();
            if (defaults != null) ApplyExposureDefaults(ExposureConfig, defaults);

            MudRakeConfig = new MudRakeConfig();
            if (defaults != null) ApplyMudRakeDefaults(MudRakeConfig, defaults);

            if (log)
            {
                Mod.Logger.Notification(
                    $"Drying frame config: rain={Config.EnableRainRot} (x{Config.RainRotMultiplier}), " +
                    $"wind={Config.EnableWindDrying} (x{Config.WindDryMultiplier}), " +
                    $"interval={Config.WeatherCheckIntervalMs}ms");

                Mod.Logger.Notification(
                    $"Exposure config: enabled={ExposureConfig.Enabled}, " +
                    $"heat={ExposureConfig.HeatThreshold}C, cold={ExposureConfig.ColdThreshold}C, " +
                    $"rate={ExposureConfig.AccumulationRatePerHour}/hr");
            }
        }

        private JsonObject? LoadAssetDefaults(ICoreAPI api)
        {
            var asset = api.Assets.TryGet(new AssetLocation("seafarer:config/seafarer-defaults.json"));
            if (asset == null) return null;
            try
            {
                return new JsonObject(Newtonsoft.Json.Linq.JToken.Parse(asset.ToText()));
            }
            catch
            {
                return null;
            }
        }

        private static void ApplyDryingFrameDefaults(DryingFrameConfig cfg, JsonObject d)
        {
            var s = d["dryingFrame"];
            if (!s.Exists) return;
            cfg.EnableRainRot = s["enableRainRot"].AsBool(cfg.EnableRainRot);
            cfg.EnableWindDrying = s["enableWindDrying"].AsBool(cfg.EnableWindDrying);
            cfg.RainRotMultiplier = s["rainRotMultiplier"].AsFloat(cfg.RainRotMultiplier);
            cfg.WindDryMultiplier = s["windDryMultiplier"].AsFloat(cfg.WindDryMultiplier);
            cfg.DryingSpeedMultiplier = s["dryingSpeedMultiplier"].AsFloat(cfg.DryingSpeedMultiplier);
            cfg.WeatherCheckIntervalMs = s["weatherCheckIntervalMs"].AsInt(cfg.WeatherCheckIntervalMs);
            cfg.RainThreshold = s["rainThreshold"].AsFloat(cfg.RainThreshold);
        }

        private static void ApplyGriddleDefaults(GriddleConfig cfg, JsonObject d)
        {
            var s = d["griddle"];
            if (!s.Exists) return;
            cfg.CookingTickIntervalMs = s["cookingTickIntervalMs"].AsInt(cfg.CookingTickIntervalMs);
            cfg.CookSpeedMultipliers["clay"] = s["cookSpeedClay"].AsFloat(cfg.CookSpeedMultipliers["clay"]);
            cfg.CookSpeedMultipliers["copper"] = s["cookSpeedCopper"].AsFloat(cfg.CookSpeedMultipliers["copper"]);
            cfg.CookSpeedMultipliers["bronze"] = s["cookSpeedBronze"].AsFloat(cfg.CookSpeedMultipliers["bronze"]);
            cfg.CookSpeedMultipliers["tinbronze"] = s["cookSpeedBronze"].AsFloat(cfg.CookSpeedMultipliers["tinbronze"]);
            cfg.CookSpeedMultipliers["bismuthbronze"] = s["cookSpeedBronze"].AsFloat(cfg.CookSpeedMultipliers["bismuthbronze"]);
            cfg.CookSpeedMultipliers["blackbronze"] = s["cookSpeedBronze"].AsFloat(cfg.CookSpeedMultipliers["blackbronze"]);
            cfg.CookSpeedMultipliers["iron"] = s["cookSpeedIron"].AsFloat(cfg.CookSpeedMultipliers["iron"]);
            cfg.CookSpeedMultipliers["steel"] = s["cookSpeedSteel"].AsFloat(cfg.CookSpeedMultipliers["steel"]);
        }

        private static void ApplySaltPanDefaults(SaltPanConfig cfg, JsonObject d)
        {
            var s = d["saltPan"];
            if (!s.Exists) return;
            cfg.Enabled = s["enabled"].AsBool(cfg.Enabled);
            cfg.CapacityLitres = s["capacityLitres"].AsFloat(cfg.CapacityLitres);
            cfg.BaseEvapRatePerHour = s["baseEvapRatePerHour"].AsFloat(cfg.BaseEvapRatePerHour);
            cfg.SaltYieldPerLitre = s["saltYieldPerLitre"].AsFloat(cfg.SaltYieldPerLitre);
            cfg.WeatherCheckIntervalMs = s["weatherCheckIntervalMs"].AsInt(cfg.WeatherCheckIntervalMs);
            cfg.RainThreshold = s["rainThreshold"].AsFloat(cfg.RainThreshold);
            cfg.MinEvaporationTemperature = s["minEvaporationTemperature"].AsFloat(cfg.MinEvaporationTemperature);
            cfg.TemperatureScaleBase = s["temperatureScaleBase"].AsFloat(cfg.TemperatureScaleBase);
            cfg.MaxTemperatureMultiplier = s["maxTemperatureMultiplier"].AsFloat(cfg.MaxTemperatureMultiplier);
        }

        private static void ApplyExposureDefaults(ExposureConfig cfg, JsonObject d)
        {
            var s = d["exposure"];
            if (!s.Exists) return;
            cfg.Enabled = s["enabled"].AsBool(cfg.Enabled);
            cfg.HeatstrokeEnabled = s["heatstrokeEnabled"].AsBool(cfg.HeatstrokeEnabled);
            cfg.FrostbiteEnabled = s["frostbiteEnabled"].AsBool(cfg.FrostbiteEnabled);
            cfg.HeatThreshold = s["heatThreshold"].AsFloat(cfg.HeatThreshold);
            cfg.ColdThreshold = s["coldThreshold"].AsFloat(cfg.ColdThreshold);
            cfg.AccumulationRatePerHour = s["accumulationRatePerHour"].AsFloat(cfg.AccumulationRatePerHour);
            cfg.DecayRatePerHour = s["decayRatePerHour"].AsFloat(cfg.DecayRatePerHour);
            cfg.WindAccumulationMultiplier = s["windAccumulationMultiplier"].AsFloat(cfg.WindAccumulationMultiplier);
            cfg.HealingExposureReduction = s["healingExposureReduction"].AsFloat(cfg.HealingExposureReduction);
            cfg.Tier1Threshold = s["tier1Threshold"].AsFloat(cfg.Tier1Threshold);
            cfg.Tier2Threshold = s["tier2Threshold"].AsFloat(cfg.Tier2Threshold);
            cfg.Tier3Threshold = s["tier3Threshold"].AsFloat(cfg.Tier3Threshold);
            cfg.HeatstrokeT1SpeedPenalty = s["heatstrokeT1SpeedPenalty"].AsFloat(cfg.HeatstrokeT1SpeedPenalty);
            cfg.HeatstrokeT1DamageTakenIncrease = s["heatstrokeT1DamageTakenIncrease"].AsFloat(cfg.HeatstrokeT1DamageTakenIncrease);
            cfg.HeatstrokeT2HungerRateMultiplier = s["heatstrokeT2HungerRateMultiplier"].AsFloat(cfg.HeatstrokeT2HungerRateMultiplier);
            cfg.HeatstrokeT2SatietyPenalty = s["heatstrokeT2SatietyPenalty"].AsFloat(cfg.HeatstrokeT2SatietyPenalty);
            cfg.HeatstrokeT3SpeedPenalty = s["heatstrokeT3SpeedPenalty"].AsFloat(cfg.HeatstrokeT3SpeedPenalty);
            cfg.HeatstrokeT3DamagePerTick = s["heatstrokeT3DamagePerTick"].AsFloat(cfg.HeatstrokeT3DamagePerTick);
            cfg.HeatstrokeT3StabilityOffset = s["heatstrokeT3StabilityOffset"].AsDouble(cfg.HeatstrokeT3StabilityOffset);
            cfg.FrostbiteT1MaxHealthReduction = s["frostbiteT1MaxHealthReduction"].AsFloat(cfg.FrostbiteT1MaxHealthReduction);
            cfg.FrostbiteT2MaxHealthReduction = s["frostbiteT2MaxHealthReduction"].AsFloat(cfg.FrostbiteT2MaxHealthReduction);
            cfg.FrostbiteT2ItemDropChance = s["frostbiteT2ItemDropChance"].AsFloat(cfg.FrostbiteT2ItemDropChance);
            cfg.FrostbiteT3MaxHealthReduction = s["frostbiteT3MaxHealthReduction"].AsFloat(cfg.FrostbiteT3MaxHealthReduction);
            cfg.FrostbiteT3ItemDropChance = s["frostbiteT3ItemDropChance"].AsFloat(cfg.FrostbiteT3ItemDropChance);
            cfg.FrostbiteT3DamagePerTick = s["frostbiteT3DamagePerTick"].AsFloat(cfg.FrostbiteT3DamagePerTick);
            cfg.HodThirstMultiplierPerTier = s["hodThirstMultiplierPerTier"].AsFloat(cfg.HodThirstMultiplierPerTier);
        }

        private static void ApplyMudRakeDefaults(MudRakeConfig cfg, JsonObject d)
        {
            var s = d["mudRake"];
            if (!s.Exists) return;
            cfg.DropRollsPerBlock = s["dropRollsPerBlock"].AsInt(cfg.DropRollsPerBlock);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("seafarer:hello"));

            api.ModLoader.GetModSystem<CharacterExtraDialogs>().OnEnvText += OnExposureEnvText;
            clientApi = api;
        }

        private ICoreClientAPI? clientApi;

        private void OnExposureEnvText(StringBuilder sb)
        {
            var plr = clientApi?.World?.Player?.Entity;
            if (plr == null) return;

            var expTree = plr.WatchedAttributes.GetTreeAttribute("exposure");
            if (expTree == null) return;

            float level = expTree.GetFloat("level");
            if (level <= 0f) return;

            var condition = (ExposureCondition)expTree.GetInt("condition");
            int tier = expTree.GetInt("tier");

            string conditionName;
            if (tier > 0 && condition != ExposureCondition.None)
            {
                conditionName = condition switch
                {
                    ExposureCondition.Heatstroke => Lang.Get("seafarer:exposure-heatstroke-" + tier),
                    ExposureCondition.Frostbite => Lang.Get("seafarer:exposure-frostbite-" + tier),
                    _ => ""
                };
            }
            else
            {
                // Exposure building but not yet at tier 1 threshold
                conditionName = condition switch
                {
                    ExposureCondition.Heatstroke => Lang.Get("seafarer:exposure-building-heat"),
                    ExposureCondition.Frostbite => Lang.Get("seafarer:exposure-building-cold"),
                    _ => ""
                };
            }

            sb.AppendLine();
            sb.Append(Lang.Get("seafarer:exposure-label", (int)(level * 100), conditionName));
        }
    }
}
