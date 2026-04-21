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
            DetermineSeafarerStoryStructures();
            strucRand.SetWorldSeed(api.WorldManager.Seed ^ 2389173L);
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

            // Resolve skip-generation-categories to SHA-hashed flags so base-game worldgen
            // can match against our radii.
            foreach (var def in scfg.Structures)
            {
                if (def.SkipGenerationCategories != null)
                {
                    def.SkipGenerationFlags = new Dictionary<int, int>();
                    foreach (var category in def.SkipGenerationCategories)
                    {
                        int key = BitConverter.ToInt32(
                            System.Security.Cryptography.SHA256.HashData(
                                System.Text.Encoding.UTF8.GetBytes(category.Key.ToLowerInvariant())));
                        def.SkipGenerationFlags.Add(key, category.Value);
                    }
                }
            }

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

            // Validate dependsOnStructure ordering: a dep must be "spawn" or refer to a code
            // that appears earlier in the array. DetermineSeafarerStoryStructures iterates in
            // array order and looks up deps in storyLocations, so later-declared deps will
            // silently fail. Warn here so config authors catch the problem before runtime.
            var seenCodes = new HashSet<string>();
            foreach (var def in scfg.Structures)
            {
                if (!string.IsNullOrEmpty(def.DependsOnStructure)
                    && def.DependsOnStructure != "spawn"
                    && !seenCodes.Contains(def.DependsOnStructure))
                {
                    api.Logger.Warning(
                        "Seafarer structure '{0}' depends on '{1}' which is not declared before it in the config. Reorder the config so dependencies come first, or determination will fail.",
                        def.Code, def.DependsOnStructure);
                }
                seenCodes.Add(def.Code);
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

        private const int MaxDeterminationAttempts = 30;

        protected void DetermineSeafarerStoryStructures()
        {
            BlockPos spawnPos;
            var df = api.WorldManager.SaveGame.DefaultSpawn;
            if (df != null)
            {
                spawnPos = new BlockPos(df.x, df.y ?? 0, df.z, 0);
            }
            else
            {
                spawnPos = api.World.BlockAccessor.MapSize.AsBlockPos / 2;
            }

            int i = 0;
            foreach (var def in scfg.Structures)
            {
                if (!def.StoryStructure) { i++; continue; }

                // Already determined (persisted from prior load)
                if (storyLocations.TryGetValue(def.Code, out var existing))
                {
                    // refresh runtime-tunable fields in case the asset was edited
                    existing.LandformRadius = def.LandformRadius;
                    existing.GenerationRadius = def.GenerationRadius;
                    existing.SkipGenerationFlags = def.SkipGenerationFlags;
                    if (existing.SkipGenerationFlags != null && existing.SkipGenerationFlags.Count > 0)
                    {
                        int max = existing.SkipGenerationFlags.Max(f => f.Value);
                        existing.MaxSkipGenerationRadiusSq = max * max;
                    }
                    i++;
                    continue;
                }

                if (attemptedCodes.Contains(def.Code)) { i++; continue; }

                strucRand.SetWorldSeed(api.WorldManager.Seed + 2389173 + i);
                TryDetermineLocation(def, spawnPos);
                attemptedCodes.Add(def.Code);
                i++;
            }

            LocationsDirty = true;
        }

        private void TryDetermineLocation(SeafarerStructure def, BlockPos spawnPos)
        {
            BlockPos basePos = spawnPos;
            if (!string.IsNullOrEmpty(def.DependsOnStructure) && def.DependsOnStructure != "spawn")
            {
                if (storyLocations.TryGetValue(def.DependsOnStructure, out var dep))
                {
                    basePos = dep.CenterPos.Copy();
                }
                else
                {
                    FailedToGenerateLocation = true;
                    api.Logger.Error(
                        "Seafarer structure '{0}' depends on '{1}', which was not found. Use /wgen seafarer setpos to place it manually.",
                        def.Code, def.DependsOnStructure);
                    return;
                }
            }

            if (def.schematicData == null)
            {
                api.Logger.Error("Seafarer structure '{0}': no schematic data, skipping determination.", def.Code);
                FailedToGenerateLocation = true;
                return;
            }

            int dirX = strucRand.NextFloat() > 0.5 ? -1 : 1;
            int dirZ = strucRand.NextFloat() > 0.5 ? -1 : 1;

            for (int attempt = 0; attempt < MaxDeterminationAttempts; attempt++)
            {
                int distanceX = def.MinSpawnDistX + strucRand.NextInt(Math.Max(1, def.MaxSpawnDistX + 1 - def.MinSpawnDistX));
                int distanceZ = def.MinSpawnDistZ + strucRand.NextInt(Math.Max(1, def.MaxSpawnDistZ + 1 - def.MinSpawnDistZ));

                int px = basePos.X + distanceX * dirX;
                int pz = basePos.Z + distanceZ * dirZ;
                int py = def.Placement == EnumSeafarerPlacement.Surface || def.Placement == EnumSeafarerPlacement.OceanSurface
                    ? api.World.SeaLevel + def.schematicData.OffsetY
                    : 1;

                var pos = new BlockPos(px, py, pz, 0);
                int schemX = def.schematicData.SizeX;
                int schemZ = def.schematicData.SizeZ;
                int minX = pos.X - schemX / 2;
                int minZ = pos.Z - schemZ / 2;
                var loc = new Cuboidi(minX, pos.Y, minZ, minX + schemX, pos.Y + def.schematicData.SizeY, minZ + schemZ);

                // NOTE: Ocean validation is deferred to per-chunk Y-resolution (see Task 5).
                // Candidate accepted on first attempt; if fine-grained validation fails later,
                // the reservation is dropped and the structure is reported via listmissing.
                var entry = new SeafarerStructureLocation
                {
                    Code = def.Code,
                    CenterPos = pos,
                    Location = loc,
                    LandformRadius = def.LandformRadius,
                    GenerationRadius = def.GenerationRadius,
                    DirX = dirX,
                    SkipGenerationFlags = def.SkipGenerationFlags
                };
                if (entry.SkipGenerationFlags != null && entry.SkipGenerationFlags.Count > 0)
                {
                    int max = entry.SkipGenerationFlags.Max(f => f.Value);
                    entry.MaxSkipGenerationRadiusSq = max * max;
                }

                storyLocations[def.Code] = entry;
                api.Logger.Notification("Seafarer story structure '{0}' determined at ({1}, {2}, {3})", def.Code, pos.X, pos.Y, pos.Z);
                return;
            }

            FailedToGenerateLocation = true;
            api.Logger.Warning("Seafarer story structure '{0}': no valid candidate found after {1} attempts.", def.Code, MaxDeterminationAttempts);
        }

        public List<string> GetMissingStructures()
        {
            var missing = new List<string>();
            foreach (var code in attemptedCodes)
            {
                if (!storyLocations.ContainsKey(code)) missing.Add(code);
            }
            return missing;
        }
    }
}
