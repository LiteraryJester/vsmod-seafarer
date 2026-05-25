using HarmonyLib;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Seafarer
{
    public class SeafarerModSystem : ModSystem
    {
        public const string ConfigFilename = "SaltAndSandConfig.json";
        private const string HarmonyId = "seafarer";

        public static DryingFrameConfig Config { get; private set; } = new();
        public static SaltPanConfig SaltPanConfig { get; private set; } = new();
        public static GriddleConfig GriddleConfig { get; private set; } = new();
        public static MudRakeConfig MudRakeConfig { get; private set; } = new();
        public static BoatConfig BoatConfig { get; private set; } = new();

        private Harmony? harmony;
        private static bool patchesApplied;

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
            api.RegisterEntityBehaviorClass("shipmechanics", typeof(EntityBehaviorShipMechanics));
            api.RegisterEntity("EntityProjectileBarbed", typeof(EntityProjectileBarbed));
            api.RegisterItemClass("ItemOceanLocatorMap", typeof(ItemOceanLocatorMap));
            api.RegisterEntity("EntityOutriggerBoat", typeof(EntityOutriggerBoat));
            api.RegisterItemClass("ItemOutriggerBoat", typeof(ItemOutriggerBoat));
            api.RegisterItemClass("ItemOutriggerRollers", typeof(ItemOutriggerRollers));

            // PatchAll runs once per process. Start fires for both server and
            // client ModSystem instances in single-player; without this guard
            // every patched method gets its postfix/prefix invoked twice.
            if (!patchesApplied)
            {
                harmony = new Harmony(HarmonyId);
                harmony.PatchAll(typeof(SeafarerModSystem).Assembly);
                patchesApplied = true;
            }
        }

        public override void Dispose()
        {
            if (harmony != null)
            {
                harmony.UnpatchAll(HarmonyId);
                patchesApplied = false;
            }
            base.Dispose();
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);
            BoatTraitRegistry.Load(api);
            if (api is ICoreServerAPI sapi)
            {
                LoadConfigs(sapi);
                ApplyBoatSpeedOverrides(sapi);
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
                if (domain == "seafarer")
                {
                    LoadConfigs(api);
                    ApplyBoatSpeedOverrides(api);
                }
            };
        }

        private void LoadConfigs(ICoreServerAPI api)
        {
            var defaults = LoadAssetDefaults(api);

            Config = new DryingFrameConfig();
            if (defaults != null) ApplyDryingFrameDefaults(Config, defaults);

            GriddleConfig = new GriddleConfig();
            if (defaults != null) ApplyGriddleDefaults(GriddleConfig, defaults);

            SaltPanConfig = new SaltPanConfig();
            if (defaults != null) ApplySaltPanDefaults(SaltPanConfig, defaults);

            MudRakeConfig = new MudRakeConfig();
            if (defaults != null) ApplyMudRakeDefaults(MudRakeConfig, defaults);

            BoatConfig = new BoatConfig();
            if (defaults != null) ApplyBoatDefaults(BoatConfig, defaults);
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

        private static void ApplyMudRakeDefaults(MudRakeConfig cfg, JsonObject d)
        {
            var s = d["mudRake"];
            if (!s.Exists) return;
            cfg.DropRollsPerBlock = s["dropRollsPerBlock"].AsInt(cfg.DropRollsPerBlock);
        }

        private static void ApplyBoatDefaults(BoatConfig cfg, JsonObject d)
        {
            var s = d["boats"];
            if (!s.Exists) return;
            cfg.LogbargeSpeedMultiplier = s["logbargeSpeedMultiplier"].AsFloat(cfg.LogbargeSpeedMultiplier);
            cfg.OutriggerSpeedMultiplier = s["outriggerSpeedMultiplier"].AsFloat(cfg.OutriggerSpeedMultiplier);
        }

        // Overwrites the speedMultiplier attribute on each boat EntityProperties so
        // EntityBoat.Initialize picks up the configured value the next time a boat is
        // spawned or loaded. Already-spawned boats keep their captured value until reload.
        private static void ApplyBoatSpeedOverrides(ICoreAPI api)
        {
            foreach (var props in api.World.EntityTypes)
            {
                if (props.Code.Domain != "seafarer") continue;
                float? newValue = props.Code.Path switch
                {
                    var p when p.StartsWith("boat-logbarge") => BoatConfig.LogbargeSpeedMultiplier,
                    var p when p.StartsWith("boat-outrigger") => BoatConfig.OutriggerSpeedMultiplier,
                    _ => null
                };
                if (newValue == null) continue;
                if (props.Attributes?.Token is not JObject attrs) continue;
                attrs["speedMultiplier"] = newValue.Value;
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            // One-time per-join migration: the exposure system
            // (EntityBehaviorExposure + ExposureConfig + the
            // exposure-player-behavior.json patch) was removed in commit
            // 323f5bf. Old saves still carry its persisted state on player
            // entities — a "exposure" watched-attribute tree and stat
            // modifiers under the "exposurePenalty" key — so without a
            // scrub a returning player keeps the reduced walkspeed,
            // healing effectiveness, hunger rate, max-health bonus, and
            // thirst multiplier from whatever tier they were at when the
            // system was removed.
            //
            // Idempotent: WatchedAttributes.RemoveAttribute and
            // entity.Stats.Remove on missing keys are no-ops. Logs once
            // per migrated player and stays silent for fresh saves.
            api.Event.PlayerJoin += player => ScrubLegacyExposure(api, player);
        }

        private static void ScrubLegacyExposure(ICoreServerAPI api, IServerPlayer player)
        {
            if (player?.Entity == null) return;
            var entity = player.Entity;

            // Five stat modifiers from the removed RemoveAllStatModifiers.
            // Safe to call when absent.
            entity.Stats.Remove("walkspeed", "exposurePenalty");
            entity.Stats.Remove("healingeffectivness", "exposurePenalty");
            entity.Stats.Remove("hungerrate", "exposurePenalty");
            entity.Stats.Remove("maxhealthExtraPoints", "exposurePenalty");
            entity.Stats.Remove("thirstRateMul", "exposurePenalty");

            if (entity.WatchedAttributes.HasAttribute("exposure"))
            {
                entity.WatchedAttributes.RemoveAttribute("exposure");
                entity.WatchedAttributes.MarkPathDirty("exposure");
                api.Logger.Notification(
                    "[Seafarer] Cleared legacy exposure state from player {0}.",
                    player.PlayerName);
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("seafarer:hello"));

            // Mirror the server-side channel registered in
            // GenSeafarerStructures.StartServerSide. That class is
            // server-only (ShouldLoad => side == Server), so the client
            // never gets the matching registration there and the engine
            // logs "Server sends me channel name SeafarerGenFailed, but
            // no client side mod registered it." Register the same
            // channel + message type here and surface the missing
            // structures to the player as a chat message.
            api.Network
                .RegisterChannel("SeafarerGenFailed")
                .RegisterMessageType<Seafarer.WorldGen.SeafarerGenFailed>()
                .SetMessageHandler<Seafarer.WorldGen.SeafarerGenFailed>(packet =>
                {
                    if (packet?.MissingStructures == null || packet.MissingStructures.Count == 0) return;
                    string list = string.Join(", ", packet.MissingStructures);
                    api.ShowChatMessage($"[Seafarer] Worldgen could not place: {list}");
                });
        }
    }
}
