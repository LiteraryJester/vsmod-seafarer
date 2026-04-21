using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;
using VsOrderedDictionary = Vintagestory.API.Datastructures.OrderedDictionary<string, Seafarer.WorldGen.SeafarerStructureLocation>;

#nullable disable

namespace Seafarer.WorldGen
{
    public enum EnumSeafarerPlacement
    {
        Surface,
        SurfaceRuin,
        Coastal,
        Underwater,
        OceanSurface
    }

    public class SeafarerStructure : WorldGenStructureBase
    {
        // Intentionally shadows WorldGenStructureBase.Placement to widen the enum from
        // EnumStructurePlacement (Surface/SurfaceRuin/Underground/Underwater) to the
        // Seafarer-specific set (adds Coastal, OceanSurface). JSON key `placement` binds
        // here because Newtonsoft resolves to the most-derived member by name.
        [JsonProperty]
        public new EnumSeafarerPlacement Placement = EnumSeafarerPlacement.Surface;
        [JsonProperty]
        public string Group;
        [JsonProperty]
        public bool StoryStructure = false;
        [JsonProperty]
        public float Chance = 0f;
        [JsonProperty]
        public bool ClearFootprint = false;

        [JsonProperty]
        public bool RequireOcean = false;
        [JsonProperty]
        public bool RequireCoast = false;
        [JsonProperty]
        public int MinWaterDepth = 0;
        [JsonProperty]
        public int MaxWaterDepth = 255;

        [JsonProperty]
        public string DependsOnStructure;
        [JsonProperty]
        public int MinSpawnDistX;
        [JsonProperty]
        public int MaxSpawnDistX;
        [JsonProperty]
        public int MinSpawnDistZ;
        [JsonProperty]
        public int MaxSpawnDistZ;

        [JsonProperty]
        public string RequireLandform;
        [JsonProperty]
        public int LandformRadius;
        [JsonProperty]
        public int GenerationRadius;
        [JsonProperty]
        public int? ForceRain;
        [JsonProperty]
        public int? ForceTemperature;

        [JsonProperty]
        public Dictionary<string, int> SkipGenerationCategories;
        public Dictionary<int, int> SkipGenerationFlags;

        [JsonProperty]
        public bool UseWorldgenHeight;
        [JsonProperty]
        public bool DisableSurfaceTerrainBlending;
        [JsonProperty]
        public bool GenerateGrass;
        [JsonProperty]
        public bool SuppressTrees;
        [JsonProperty]
        public bool RandomRotation = true;

        [JsonProperty]
        public bool ExcludeSchematicSizeProtect;
        [JsonProperty]
        public int ExtraLandClaimX;
        [JsonProperty]
        public int ExtraLandClaimZ;
        [JsonProperty]
        public Cuboidi[] CustomLandClaims;

        [JsonProperty]
        public string[] PostPlaceDecorators = Array.Empty<string>();

        internal BlockSchematicPartial schematicData;
    }

    public class SeafarerStructuresConfig
    {
        public Dictionary<string, int> SchematicYOffsets = new();
        public Dictionary<string, Dictionary<AssetLocation, AssetLocation>> RocktypeRemapGroups = new();
        public SeafarerStructure[] Structures = Array.Empty<SeafarerStructure>();
    }

    [ProtoContract]
    public class SeafarerStructureLocation
    {
        [ProtoMember(1)] public string Code;
        [ProtoMember(2)] public BlockPos CenterPos;
        [ProtoMember(3)] public Cuboidi Location;
        [ProtoMember(4)] public int LandformRadius;
        [ProtoMember(5)] public int GenerationRadius;
        [ProtoMember(6)] public int DirX;
        [ProtoMember(7)] public Dictionary<int, int> SkipGenerationFlags;
        [ProtoMember(8)] public bool DidGenerate;
        [ProtoMember(9)] public int WorldgenHeight = -1;
        [ProtoMember(10)] public string RockBlockCode;
        [ProtoMember(11)] public bool OceanValidated;

        public int MaxSkipGenerationRadiusSq;
    }

    public class SeafarerStructurePlacedEvent
    {
        public SeafarerStructure Def;
        public string Code;
        public int ChunkX;
        public int ChunkZ;
        public Cuboidi Bounds;
        public int BlocksPlacedInSlice;
    }

    [ProtoContract]
    public class SeafarerGenFailed
    {
        [ProtoMember(1)]
        public List<string> MissingStructures;
    }

    public class GenSeafarerStructures : ModStdWorldGen
    {
        public SeafarerStructuresConfig scfg = new();

        protected ICoreServerAPI api;
        protected LCGRandom strucRand;
        protected LCGRandom grassRand;
        protected IWorldGenBlockAccessor worldgenBlockAccessor;
        protected BlockLayerConfig blockLayerConfig;
        protected bool FailedToGenerateLocation;
        protected IServerNetworkChannel serverChannel;

        protected readonly VsOrderedDictionary storyLocations = new();
        protected readonly List<string> attemptedCodes = new();
        protected bool LocationsDirty;

        /// <summary>Look up a story structure location by code. Returns null if not found.</summary>
        public SeafarerStructureLocation GetLocation(string code)
        {
            return storyLocations.TryGetValue(code, out var loc) ? loc : null;
        }

        /// <summary>Enumerate all story structure locations in insertion order.</summary>
        public IEnumerable<KeyValuePair<string, SeafarerStructureLocation>> EnumerateLocations()
        {
            return storyLocations;
        }
        public event Action<SeafarerStructurePlacedEvent> OnStructurePlaced;

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
        public override double ExecuteOrder() => 0.21;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            base.StartServerSide(api);

            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.InitWorldGenerator(InitWorldGen, "standard");
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Vegetation, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }

            serverChannel = api.Network.RegisterChannel("SeafarerGenFailed");
            serverChannel.RegisterMessageType<SeafarerGenFailed>();
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
        }

        public void InitWorldGen()
        {
            strucRand = new LCGRandom(api.WorldManager.Seed ^ 2389173L);
            grassRand = new LCGRandom(api.WorldManager.Seed);
            blockLayerConfig = BlockLayerConfig.GetInstance(api);

            LoadConfig();
        }

        private void LoadConfig()
        {
            var assets = api.Assets.GetMany<SeafarerStructuresConfig>(api.Logger, "worldgen/seafarerstructures.json");

            scfg = new SeafarerStructuresConfig();
            var structures = new List<SeafarerStructure>();

            foreach (var (_, conf) in assets)
            {
                foreach (var remap in conf.RocktypeRemapGroups)
                {
                    scfg.RocktypeRemapGroups.TryAdd(remap.Key, remap.Value);
                }
                foreach (var offset in conf.SchematicYOffsets)
                {
                    scfg.SchematicYOffsets.TryAdd(offset.Key, offset.Value);
                }
                if (conf.Structures != null) structures.AddRange(conf.Structures);
            }

            scfg.Structures = structures.ToArray();

            foreach (var def in scfg.Structures)
            {
                if (def.Schematics == null || def.Schematics.Length == 0) continue;
                try
                {
                    def.schematicData = def.LoadSchematics<BlockSchematicPartial>(api, def.Schematics, null)[0];
                    def.schematicData.blockLayerConfig = blockLayerConfig;

                    if (scfg.SchematicYOffsets.TryGetValue(
                        "story/" + def.schematicData.FromFile.GetNameWithDomain().Replace(".json", ""),
                        out var off))
                    {
                        def.schematicData.OffsetY = off;
                    }
                }
                catch (Exception e)
                {
                    api.Logger.Error("Seafarer structure '{0}': schematic load failed: {1}", def.Code, e.Message);
                }
            }

            api.Logger.Notification("Seafarer structures: loaded {0} definitions.", scfg.Structures.Length);
        }

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            // Story + scattered placement wired up in later tasks.
        }

        private void Event_SaveGameLoaded()
        {
            storyLocations.Clear();
            attemptedCodes.Clear();

            var locData = api.WorldManager.SaveGame.GetData<VsOrderedDictionary>("seafarer-structure-locations");
            if (locData != null)
            {
                foreach (var kv in locData) storyLocations[kv.Key] = kv.Value;
            }

            var attempted = api.WorldManager.SaveGame.GetData<List<string>>("attemptedToGenerateSeafarerLocation");
            if (attempted != null) attemptedCodes.AddRange(attempted);
        }

        private void Event_GameWorldSave()
        {
            if (LocationsDirty)
            {
                api.WorldManager.SaveGame.StoreData("seafarer-structure-locations", SerializerUtil.Serialize(storyLocations));
                LocationsDirty = false;
            }
            api.WorldManager.SaveGame.StoreData("attemptedToGenerateSeafarerLocation", attemptedCodes);
        }

        protected void TriggerOnStructurePlaced(SeafarerStructure def, int chunkX, int chunkZ, Cuboidi bounds, int blocks)
        {
            OnStructurePlaced?.Invoke(new SeafarerStructurePlacedEvent
            {
                Def = def,
                Code = def.Code,
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                Bounds = bounds,
                BlocksPlacedInSlice = blocks
            });
        }
    }
}
