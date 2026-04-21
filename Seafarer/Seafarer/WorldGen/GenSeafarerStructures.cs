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

        private readonly List<string> pendingDrops = new();

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

            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands.GetOrCreate("wgen")
                .BeginSubCommand("seafarer")
                    .BeginSubCommand("tp")
                        .WithRootAlias("tpseafarerloc")
                        .WithDescription("Teleport to a Seafarer story structure")
                        .RequiresPrivilege(Privilege.controlserver)
                        .RequiresPlayer()
                        .WithArgs(parsers.Word("code"))
                        .HandleWith(OnCmdTp)
                    .EndSubCommand()
                    .BeginSubCommand("setpos")
                        .WithRootAlias("setseafarerstrucpos")
                        .WithDescription("Set the location of a Seafarer story structure")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithArgs(parsers.Word("code"), parsers.WorldPosition("position"), parsers.OptionalBool("confirm"))
                        .HandleWith(OnCmdSetPos)
                    .EndSubCommand()
                    .BeginSubCommand("listmissing")
                        .WithDescription("List Seafarer story structures that failed to determine or validate")
                        .RequiresPrivilege(Privilege.controlserver)
                        .HandleWith(OnCmdListMissing)
                    .EndSubCommand()
                    .BeginSubCommand("rmsc")
                        .WithDescription("Reset a Seafarer story structure's generation state so /wgen regen will re-place it")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithArgs(parsers.Word("code"))
                        .HandleWith(OnCmdRmsc)
                    .EndSubCommand()
                    .BeginSubCommand("place")
                        .WithDescription("Force-place a Seafarer structure at your position (debug)")
                        .RequiresPrivilege(Privilege.controlserver)
                        .RequiresPlayer()
                        .WithArgs(parsers.Word("code"))
                        .HandleWith(OnCmdPlace)
                    .EndSubCommand()
                    .BeginSubCommand("list")
                        .WithDescription("List all registered Seafarer structure codes and status")
                        .HandleWith(OnCmdList)
                    .EndSubCommand()
                .EndSubCommand();
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
                    DedupeFluidIndices(def.schematicData);
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

        private readonly Cuboidi tmpCuboid = new();

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            if (scfg == null || scfg.Structures.Length == 0) return;

            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;
            var mapRegion = chunks[0].MapChunk.MapRegion;

            tmpCuboid.Set(
                chunkX * chunksize, 0, chunkZ * chunksize,
                chunkX * chunksize + chunksize, chunks.Length * chunksize, chunkZ * chunksize + chunksize);

            worldgenBlockAccessor.BeginColumn();

            PlaceStorySlices(request, mapRegion, chunkX, chunkZ);
            PlaceScatteredRolls(request, mapRegion, chunkX, chunkZ);
        }

        private void PlaceScatteredRolls(IChunkColumnGenerateRequest request, IMapRegion mapRegion, int chunkX, int chunkZ)
        {
            var chunks = request.Chunks;
            var mapChunk = chunks[0].MapChunk;

            foreach (var def in scfg.Structures)
            {
                if (def.StoryStructure) continue;
                if (def.schematicData == null) continue;
                if (def.Chance <= 0f) continue;

                strucRand.InitPositionSeed(chunkX + StableHash(def.Code), chunkZ);
                float roll = strucRand.NextFloat();
                if (roll > def.Chance) continue;

                int localX = strucRand.NextInt(chunksize);
                int localZ = strucRand.NextInt(chunksize);
                int posX = chunkX * chunksize + localX;
                int posZ = chunkZ * chunksize + localZ;

                int terrainHeight = mapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
                int originX = posX - def.schematicData.SizeX / 2;
                int originZ = posZ - def.schematicData.SizeZ / 2;

                if (!ValidateOceanPlacement(def, mapRegion, originX, originZ,
                        def.schematicData.SizeX, def.schematicData.SizeZ, terrainHeight))
                {
                    continue;
                }

                int y = ResolveYFromTerrain(def, terrainHeight);
                var startPos = new BlockPos(originX, y, originZ, 0);
                var bounds = new Cuboidi(
                    originX, y, originZ,
                    originX + def.schematicData.SizeX, y + def.schematicData.SizeY, originZ + def.schematicData.SizeZ);

                if (def.ClearFootprint)
                {
                    ClearFootprint(chunks, bounds, chunkX, chunkZ);
                }

                int blocksPlaced = def.schematicData.PlacePartial(
                    chunks, worldgenBlockAccessor, api.World,
                    chunkX, chunkZ, startPos, EnumReplaceMode.ReplaceAll,
                    ToBaseGamePlacement(def.Placement),
                    GlobalConfig.ReplaceMetaBlocks, GlobalConfig.ReplaceMetaBlocks,
                    null, Array.Empty<int>(), null, def.DisableSurfaceTerrainBlending
                );

                if (blocksPlaced <= 0) continue;

                EmitRegionRecord(mapRegion, def, bounds);
                EmitLandClaims(def, bounds);
                TriggerOnStructurePlaced(def, chunkX, chunkZ, bounds, blocksPlaced);
            }
        }

        private void PlaceStorySlices(IChunkColumnGenerateRequest request, IMapRegion mapRegion, int chunkX, int chunkZ)
        {
            var chunks = request.Chunks;

            foreach (var (code, loc) in storyLocations)
            {
                if (!loc.Location.Intersects(tmpCuboid)) continue;

                var def = scfg.Structures.FirstOrDefault(s => s.Code == code);
                if (def == null || def.schematicData == null) continue;

                if (!loc.DidGenerate)
                {
                    loc.DidGenerate = true;
                    LocationsDirty = true;
                }

                var startPos = new BlockPos(loc.Location.X1, loc.Location.Y1, loc.Location.Z1, 0);

                if (!ResolveStoryStartY(def, loc, request, ref startPos)) continue;

                if (def.ClearFootprint)
                {
                    ClearFootprint(chunks, loc.Location, chunkX, chunkZ);
                }

                int blocksPlaced = def.schematicData.PlacePartial(
                    chunks, worldgenBlockAccessor, api.World,
                    chunkX, chunkZ, startPos, EnumReplaceMode.ReplaceAll,
                    ToBaseGamePlacement(def.Placement),
                    GlobalConfig.ReplaceMetaBlocks, GlobalConfig.ReplaceMetaBlocks,
                    null, Array.Empty<int>(), null, def.DisableSurfaceTerrainBlending
                );

                if (blocksPlaced <= 0) continue;

                EmitRegionRecord(mapRegion, def, loc.Location);
                EmitLandClaims(def, loc.Location);

                TriggerOnStructurePlaced(def, chunkX, chunkZ, loc.Location, blocksPlaced);
            }

            if (pendingDrops.Count > 0)
            {
                foreach (var code in pendingDrops) storyLocations.Remove(code);
                pendingDrops.Clear();
            }
        }

        /// <summary>
        /// Resolves Y for a story-structure chunk slice. Returns false if Y is not yet
        /// resolvable (e.g. the origin chunk hasn't generated yet).
        /// </summary>
        private bool ResolveStoryStartY(SeafarerStructure def, SeafarerStructureLocation loc, IChunkColumnGenerateRequest request, ref BlockPos startPos)
        {
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;

            bool needsTerrainY = def.Placement is EnumSeafarerPlacement.SurfaceRuin
                                 or EnumSeafarerPlacement.Coastal
                                 or EnumSeafarerPlacement.Underwater
                                 || def.UseWorldgenHeight;

            if (!needsTerrainY)
            {
                if (def.Placement == EnumSeafarerPlacement.OceanSurface && !loc.OceanValidated)
                {
                    int originChunkX0 = (int)Math.Floor((double)loc.Location.X1 / chunksize);
                    int originChunkZ0 = (int)Math.Floor((double)loc.Location.Z1 / chunksize);
                    if (chunkX != originChunkX0 || chunkZ != originChunkZ0) return false;

                    int localX0 = loc.Location.X1 - originChunkX0 * chunksize;
                    int localZ0 = loc.Location.Z1 - originChunkZ0 * chunksize;
                    if (localX0 < 0 || localX0 >= chunksize || localZ0 < 0 || localZ0 >= chunksize) return false;

                    var mr = request.Chunks[0].MapChunk.MapRegion;
                    int th = request.Chunks[0].MapChunk.WorldGenTerrainHeightMap[localZ0 * chunksize + localX0];
                    if (!ValidateOceanPlacement(def, mr, loc.Location.X1, loc.Location.Z1,
                            def.schematicData.SizeX, def.schematicData.SizeZ, th))
                    {
                        api.Logger.Warning(
                            "Seafarer story structure '{0}': OceanSurface ocean check failed; dropping reservation.", def.Code);
                        pendingDrops.Add(def.Code);
                        FailedToGenerateLocation = true;
                        LocationsDirty = true;
                        return false;
                    }
                    loc.OceanValidated = true;
                }

                // Surface / OceanSurface: sea level anchored.
                int y = api.World.SeaLevel + def.schematicData.OffsetY;
                if (loc.Location.Y1 != y)
                {
                    loc.Location.Y1 = y;
                    loc.Location.Y2 = y + def.schematicData.SizeY;
                    LocationsDirty = true;
                }
                startPos.Y = y;
                return true;
            }

            if (loc.WorldgenHeight >= 0)
            {
                startPos.Y = ResolveYFromTerrain(def, loc.WorldgenHeight);
                return true;
            }

            // Need terrain height at origin. Only the origin chunk owns it.
            // Use floor-division so negative coordinates resolve to the correct chunk:
            // C# integer division truncates toward zero, so -50/32 = -1 instead of -2.
            int originChunkX = (int)Math.Floor((double)loc.Location.X1 / chunksize);
            int originChunkZ = (int)Math.Floor((double)loc.Location.Z1 / chunksize);
            if (chunkX != originChunkX || chunkZ != originChunkZ) return false;

            int localX = loc.Location.X1 - originChunkX * chunksize;
            int localZ = loc.Location.Z1 - originChunkZ * chunksize;
            if (localX < 0 || localX >= chunksize || localZ < 0 || localZ >= chunksize) return false;

            int terrainHeight = request.Chunks[0].MapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
            var mapRegion = request.Chunks[0].MapChunk.MapRegion;

            if (!loc.OceanValidated)
            {
                bool ok = ValidateOceanPlacement(def, mapRegion, loc.Location.X1, loc.Location.Z1,
                    def.schematicData.SizeX, def.schematicData.SizeZ, terrainHeight);
                if (!ok)
                {
                    api.Logger.Warning(
                        "Seafarer story structure '{0}': per-chunk ocean validation failed (waterDepth={1}); dropping reservation.",
                        def.Code, api.World.SeaLevel - terrainHeight);
                    pendingDrops.Add(def.Code);
                    FailedToGenerateLocation = true;
                    LocationsDirty = true;
                    return false;
                }
                loc.OceanValidated = true;
            }

            loc.WorldgenHeight = terrainHeight;
            int resolvedY = ResolveYFromTerrain(def, terrainHeight);
            loc.Location.Y1 = resolvedY;
            loc.Location.Y2 = resolvedY + def.schematicData.SizeY;
            startPos.Y = resolvedY;
            LocationsDirty = true;
            return true;
        }

        private int ResolveYFromTerrain(SeafarerStructure def, int terrainHeight)
        {
            // UseWorldgenHeight overrides placement-based Y — matches base-game precedence.
            if (def.UseWorldgenHeight)
            {
                return terrainHeight + def.schematicData.OffsetY;
            }

            return def.Placement switch
            {
                EnumSeafarerPlacement.SurfaceRuin  => terrainHeight - def.schematicData.SizeY + def.schematicData.OffsetY,
                EnumSeafarerPlacement.Coastal      => terrainHeight + def.schematicData.OffsetY,
                EnumSeafarerPlacement.Underwater   => terrainHeight + def.schematicData.OffsetY,
                EnumSeafarerPlacement.OceanSurface => api.World.SeaLevel + def.schematicData.OffsetY,
                _ /* Surface */                    => api.World.SeaLevel + def.schematicData.OffsetY,
            };
        }

        private int RegionChunkSize => api.WorldManager.RegionSize / chunksize;

        private static int StableHash(string s)
        {
            return BitConverter.ToInt32(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(s ?? string.Empty)));
        }

        private float GetOceanicity(IMapRegion mapRegion, int posX, int posZ)
        {
            if (mapRegion?.OceanMap == null || mapRegion.OceanMap.Data.Length == 0) return 0f;
            return SampleRegionMap(mapRegion.OceanMap, posX, posZ);
        }

        private float GetBeachStrength(IMapRegion mapRegion, int posX, int posZ)
        {
            if (mapRegion?.BeachMap == null || mapRegion.BeachMap.Data.Length == 0) return 0f;
            return SampleRegionMap(mapRegion.BeachMap, posX, posZ);
        }

        private float SampleRegionMap(IntDataMap2D map, int posX, int posZ)
        {
            int rlX = (posX / chunksize) % RegionChunkSize;
            int rlZ = (posZ / chunksize) % RegionChunkSize;
            if (rlX < 0) rlX += RegionChunkSize;
            if (rlZ < 0) rlZ += RegionChunkSize;

            float fac = (float)map.InnerSize / RegionChunkSize;
            int ul = map.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac));
            int ur = map.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac));
            int bl = map.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac + fac));
            int br = map.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac + fac));
            return GameMath.BiLerp(ul, ur, bl, br,
                (float)(((posX % chunksize) + chunksize) % chunksize) / chunksize,
                (float)(((posZ % chunksize) + chunksize) % chunksize) / chunksize);
        }

        /// <summary>
        /// 9-sample footprint check against OceanMap. Returns true if the candidate
        /// origin satisfies the def's ocean/coast/depth requirements.
        /// </summary>
        private bool ValidateOceanPlacement(SeafarerStructure def, IMapRegion mapRegion, int originX, int originZ, int sizeX, int sizeZ, int terrainHeight)
        {
            bool wantsOceanCheck = def.RequireOcean
                                   || def.RequireCoast
                                   || def.Placement is EnumSeafarerPlacement.Underwater or EnumSeafarerPlacement.OceanSurface
                                   || def.MinWaterDepth > 0
                                   || def.MaxWaterDepth < 255;

            if (!wantsOceanCheck) return true;

            // If OceanMap isn't populated yet (transient worldgen state), defer validation
            // rather than permanently dropping the reservation — a later chunk will re-check.
            if (mapRegion?.OceanMap == null || mapRegion.OceanMap.Data.Length == 0)
            {
                return true;
            }

            int cx = originX + sizeX / 2;
            int cz = originZ + sizeZ / 2;
            int seaLevel = api.World.SeaLevel;
            int waterDepth = seaLevel - terrainHeight;

            if (def.MinWaterDepth > 0 && waterDepth < def.MinWaterDepth) return false;
            if (def.MaxWaterDepth < 255 && waterDepth > def.MaxWaterDepth) return false;

            float centerOcean = GetOceanicity(mapRegion, cx, cz);
            bool centerIsOcean = centerOcean > 0;

            if (def.RequireCoast)
            {
                float beach = GetBeachStrength(mapRegion, cx, cz);
                bool coast = beach > 0 || (centerIsOcean && waterDepth <= def.MaxWaterDepth);
                if (!coast) return false;
            }

            if (def.RequireOcean || def.Placement is EnumSeafarerPlacement.Underwater or EnumSeafarerPlacement.OceanSurface)
            {
                if (!centerIsOcean) return false;

                int oceanSamples = 0;
                int[,] offsets = new int[8, 2]
                {
                    { originX, originZ },
                    { originX + sizeX, originZ },
                    { originX, originZ + sizeZ },
                    { originX + sizeX, originZ + sizeZ },
                    { cx, originZ },
                    { cx, originZ + sizeZ },
                    { originX, cz },
                    { originX + sizeX, cz },
                };
                for (int i = 0; i < 8; i++)
                {
                    if (GetOceanicity(mapRegion, offsets[i, 0], offsets[i, 1]) > 0) oceanSamples++;
                }
                // center + at least 6 of 8 edge samples must be ocean
                if (oceanSamples < 6) return false;
            }

            return true;
        }

        private int cachedAirBlockId = -1;
        private int AirBlockId
        {
            get
            {
                if (cachedAirBlockId == -1)
                {
                    var air = api.World.GetBlock(new AssetLocation("air"));
                    cachedAirBlockId = air?.Id ?? 0;
                }
                return cachedAirBlockId;
            }
        }

        /// <summary>
        /// Removes duplicate fluid entries from a schematic's packed Indices/BlockIds arrays.
        /// Some schematics (esp. WorldEdit-saved) end up with two entries at the same (x,y,z)
        /// position that both resolve to fluid blocks, which makes BlockSchematicStructure.Init
        /// throw because FluidBlocksByPos is populated via strict Dictionary.Add. Keeping only
        /// the first occurrence at each fluid position gives us the same last-write-wins semantic
        /// the old BlockSchematic.Place path used.
        /// </summary>
        private void DedupeFluidIndices(BlockSchematicPartial schem)
        {
            if (schem?.Indices == null || schem.BlockIds == null) return;

            var seenFluidIndices = new HashSet<uint>();
            var keptIndices = new List<uint>(schem.Indices.Count);
            var keptBlockIds = new List<int>(schem.BlockIds.Count);
            int removed = 0;

            for (int i = 0; i < schem.Indices.Count; i++)
            {
                uint idx = schem.Indices[i];
                int blockId = schem.BlockIds[i];

                if (!schem.BlockCodes.TryGetValue(blockId, out var blockCode))
                {
                    keptIndices.Add(idx);
                    keptBlockIds.Add(blockId);
                    continue;
                }

                Block block = api.World.GetBlock(blockCode);
                if (block != null && block.ForFluidsLayer)
                {
                    if (!seenFluidIndices.Add(idx))
                    {
                        removed++;
                        continue;
                    }
                }

                keptIndices.Add(idx);
                keptBlockIds.Add(blockId);
            }

            if (removed > 0)
            {
                api.Logger.Warning("Seafarer schematic had {0} duplicate fluid entries at the same position; kept first occurrence at each.", removed);
                schem.Indices = keptIndices;
                schem.BlockIds = keptBlockIds;
            }
        }

        /// <summary>
        /// Fills the structure's XYZ bounding box (clipped to the current chunk)
        /// with air on the solid layer. Runs before PlacePartial so the schematic's
        /// blocks overwrite the cleared region and interior negative space stays open.
        /// </summary>
        private void ClearFootprint(IServerChunk[] chunks, Cuboidi bounds, int chunkX, int chunkZ)
        {
            int chunkMinX = chunkX * chunksize;
            int chunkMinZ = chunkZ * chunksize;
            int clipMinX = Math.Max(bounds.X1, chunkMinX);
            int clipMaxX = Math.Min(bounds.X2, chunkMinX + chunksize);
            int clipMinZ = Math.Max(bounds.Z1, chunkMinZ);
            int clipMaxZ = Math.Min(bounds.Z2, chunkMinZ + chunksize);
            if (clipMinX >= clipMaxX || clipMinZ >= clipMaxZ) return;

            int mapSizeY = api.WorldManager.MapSizeY;
            int clipMinY = Math.Max(bounds.Y1, 0);
            int clipMaxY = Math.Min(bounds.Y2, mapSizeY);
            if (clipMinY >= clipMaxY) return;

            int air = AirBlockId;

            for (int y = clipMinY; y < clipMaxY; y++)
            {
                int chunkIndex = y / chunksize;
                if (chunkIndex < 0 || chunkIndex >= chunks.Length) continue;
                int localY = y % chunksize;
                var chunk = chunks[chunkIndex];

                for (int x = clipMinX; x < clipMaxX; x++)
                {
                    int localX = x - chunkMinX;
                    for (int z = clipMinZ; z < clipMaxZ; z++)
                    {
                        int localZ = z - chunkMinZ;
                        int blockIndex = (localY * chunksize + localZ) * chunksize + localX;
                        chunk.Data.SetBlockUnsafe(blockIndex, air);
                        chunk.Data.SetFluid(blockIndex, 0);
                    }
                }
            }
        }

        private static EnumStructurePlacement ToBaseGamePlacement(EnumSeafarerPlacement p)
        {
            return p switch
            {
                EnumSeafarerPlacement.Underwater    => EnumStructurePlacement.Underwater,
                EnumSeafarerPlacement.SurfaceRuin   => EnumStructurePlacement.SurfaceRuin,
                EnumSeafarerPlacement.Coastal       => EnumStructurePlacement.Surface,
                EnumSeafarerPlacement.OceanSurface  => EnumStructurePlacement.Surface,
                _                                   => EnumStructurePlacement.Surface,
            };
        }

        private void EmitRegionRecord(IMapRegion mapRegion, SeafarerStructure def, Cuboidi bounds)
        {
            // Story structures: dedup by code so slice re-entry across chunk-gen events
            // doesn't emit duplicate records for the same reservation.
            // Scattered structures: multiple instances per region are legitimate, do not dedup.
            if (def.StoryStructure)
            {
                var existing = mapRegion.GeneratedStructures.FirstOrDefault(g => g.Code == def.Code);
                if (existing != null) return;
            }

            mapRegion.AddGeneratedStructure(new GeneratedStructure
            {
                Code = def.Code,
                Group = "seafarerstructure",
                Location = bounds.Clone(),
                SuppressTreesAndShrubs = def.SuppressTrees,
                SuppressRivulets = true
            });
        }

        private void EmitLandClaims(SeafarerStructure def, Cuboidi bounds)
        {
            if (!def.BuildProtected) return;

            if (!def.ExcludeSchematicSizeProtect)
            {
                var claims = api.World.Claims.Get(bounds.Center.AsBlockPos);
                if (claims == null || claims.Length == 0)
                {
                    api.World.Claims.Add(new LandClaim
                    {
                        Areas = new List<Cuboidi> { bounds.Clone() },
                        Description = def.BuildProtectionDesc,
                        ProtectionLevel = def.ProtectionLevel,
                        LastKnownOwnerName = def.BuildProtectionName,
                        AllowUseEveryone = def.AllowUseEveryone,
                        AllowTraverseEveryone = def.AllowTraverseEveryone
                    });
                }
            }

            if (def.ExtraLandClaimX > 0 && def.ExtraLandClaimZ > 0)
            {
                var center = bounds.Center;
                var extra = new Cuboidi(
                    center.X - def.ExtraLandClaimX, 0, center.Z - def.ExtraLandClaimZ,
                    center.X + def.ExtraLandClaimX, api.WorldManager.MapSizeY, center.Z + def.ExtraLandClaimZ);
                var claims = api.World.Claims.Get(extra.Center.AsBlockPos);
                if (claims == null || claims.Length == 0)
                {
                    api.World.Claims.Add(new LandClaim
                    {
                        Areas = new List<Cuboidi> { extra },
                        Description = def.BuildProtectionDesc,
                        ProtectionLevel = def.ProtectionLevel,
                        LastKnownOwnerName = def.BuildProtectionName,
                        AllowUseEveryone = def.AllowUseEveryone,
                        AllowTraverseEveryone = def.AllowTraverseEveryone
                    });
                }
            }

            if (def.CustomLandClaims != null)
            {
                foreach (var shape in def.CustomLandClaims)
                {
                    var c = shape.Clone();
                    c.X1 += bounds.X1; c.X2 += bounds.X1;
                    c.Y1 += bounds.Y1; c.Y2 += bounds.Y1;
                    c.Z1 += bounds.Z1; c.Z2 += bounds.Z1;

                    var claims = api.World.Claims.Get(c.Center.AsBlockPos);
                    if (claims != null && claims.Length > 0) continue;

                    api.World.Claims.Add(new LandClaim
                    {
                        Areas = new List<Cuboidi> { c },
                        Description = def.BuildProtectionDesc,
                        ProtectionLevel = def.ProtectionLevel,
                        LastKnownOwnerName = def.BuildProtectionName,
                        AllowUseEveryone = def.AllowUseEveryone,
                        AllowTraverseEveryone = def.AllowTraverseEveryone
                    });
                }
            }
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

        /// <summary>
        /// Registers required landforms and forced climates with GenMaps so the
        /// landform map produces the correct terrain at each story-structure center.
        /// Mirrors base-game GenStoryStructures.SetupForceLandform. Must be called
        /// AFTER DetermineSeafarerStoryStructures so storyLocations is populated.
        /// </summary>
        private void SetupForceLandform()
        {
            var genmaps = api.ModLoader.GetModSystem<GenMaps>();
            if (genmaps == null)
            {
                api.Logger.Warning("Seafarer: GenMaps mod system not found; required landforms will not be forced.");
                return;
            }

            foreach (var kv in storyLocations)
            {
                var def = scfg.Structures.FirstOrDefault(s => s.Code == kv.Key);
                if (def == null)
                {
                    api.Logger.Warning("Seafarer: no config for story location '{0}'; terrain will not be forced there.", kv.Key);
                    continue;
                }
                ApplyForcedLandform(genmaps, def, kv.Value);
            }
        }

        private void ApplyForcedLandform(GenMaps genmaps, SeafarerStructure def, SeafarerStructureLocation location)
        {
            if (def.ForceTemperature != null || def.ForceRain != null)
            {
                genmaps.ForceClimateAt(new ForceClimate
                {
                    Radius = location.LandformRadius,
                    CenterPos = location.CenterPos,
                    Climate = (Climate.DescaleTemperature(def.ForceTemperature ?? 0f) << 16)
                              + ((def.ForceRain ?? 0) << 8)
                });
            }

            if (def.RequireLandform == null) return;

            try
            {
                genmaps.ForceLandformAt(new ForceLandform
                {
                    Radius = location.LandformRadius,
                    CenterPos = location.CenterPos,
                    LandformCode = def.RequireLandform
                });
            }
            catch (Exception e)
            {
                api.Logger.Error("Seafarer: ForceLandformAt failed for '{0}' with landform '{1}': {2}",
                    def.Code, def.RequireLandform, e.Message);
            }
        }

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
            SetupForceLandform();
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

        private TextCommandResult OnCmdTp(TextCommandCallingArgs args)
        {
            var code = (string)args[0];
            if (!storyLocations.TryGetValue(code, out var loc))
                return TextCommandResult.Error("No such Seafarer story structure: " + code);
            var pos = loc.CenterPos.Copy();
            pos.Y = (loc.Location.Y1 + loc.Location.Y2) / 2;
            args.Caller.Entity.TeleportTo(pos);
            return TextCommandResult.Success("Teleporting to " + code);
        }

        private TextCommandResult OnCmdSetPos(TextCommandCallingArgs args)
        {
            var code = (string)args[0];
            var def = scfg.Structures.FirstOrDefault(s => s.Code == code);
            if (def == null) return TextCommandResult.Error("No such Seafarer structure: " + code);
            if (def.schematicData == null) return TextCommandResult.Error("Structure has no schematic loaded: " + code);

            bool confirm = args[2] is bool b && b;
            var pos = ((Vec3d)args[1]).AsBlockPos;

            if (!confirm)
            {
                var chunkRange = (int)Math.Ceiling(def.LandformRadius / (float)chunksize) + 3;
                return TextCommandResult.Success(
                    $"Will move '{code}' to {pos}. Add 'true' to confirm. After, regenerate chunks in the area (e.g. /wgen delr {chunkRange}).");
            }

            int schemX = def.schematicData.SizeX;
            int schemZ = def.schematicData.SizeZ;
            int minX = pos.X - schemX / 2;
            int minZ = pos.Z - schemZ / 2;
            var cub = new Cuboidi(minX, pos.Y, minZ, minX + schemX, pos.Y + def.schematicData.SizeY, minZ + schemZ);

            var entry = new SeafarerStructureLocation
            {
                Code = code,
                CenterPos = pos,
                Location = cub,
                LandformRadius = def.LandformRadius,
                GenerationRadius = def.GenerationRadius,
                SkipGenerationFlags = def.SkipGenerationFlags,
                OceanValidated = true
            };
            if (entry.SkipGenerationFlags != null && entry.SkipGenerationFlags.Count > 0)
            {
                int max = entry.SkipGenerationFlags.Max(f => f.Value);
                entry.MaxSkipGenerationRadiusSq = max * max;
            }
            storyLocations[code] = entry;
            LocationsDirty = true;

            var genmaps = api.ModLoader.GetModSystem<GenMaps>();
            if (genmaps != null)
            {
                ApplyForcedLandform(genmaps, def, entry);
            }

            return TextCommandResult.Success($"Moved '{code}' to {pos}. Regenerate chunks in this area to materialize it.");
        }

        private TextCommandResult OnCmdListMissing(TextCommandCallingArgs args)
        {
            var missing = GetMissingStructures();
            if (missing.Count == 0) return TextCommandResult.Success("No missing Seafarer structures.");
            if (args.Caller.Player is IServerPlayer p)
            {
                serverChannel.SendPacket(new SeafarerGenFailed { MissingStructures = missing }, p);
            }
            return TextCommandResult.Success("Missing: " + string.Join(", ", missing));
        }

        private TextCommandResult OnCmdRmsc(TextCommandCallingArgs args)
        {
            var code = (string)args[0];
            if (!storyLocations.TryGetValue(code, out var loc))
                return TextCommandResult.Error("No such Seafarer story structure: " + code);
            loc.DidGenerate = false;
            loc.WorldgenHeight = -1;
            loc.OceanValidated = false;
            loc.RockBlockCode = null;
            LocationsDirty = true;
            return TextCommandResult.Success($"Reset generation state for '{code}'.");
        }

        private TextCommandResult OnCmdPlace(TextCommandCallingArgs args)
        {
            var code = (string)args[0];
            var def = scfg.Structures.FirstOrDefault(s => s.Code == code);
            if (def == null || def.schematicData == null)
                return TextCommandResult.Error("Unknown or unloaded structure: " + code);

            var player = (IServerPlayer)args.Caller.Player;
            var pos = player.CurrentBlockSelection?.Position ?? player.Entity.Pos.AsBlockPos;

            int placed = def.schematicData.Place(api.World.BlockAccessor, api.World, pos, EnumReplaceMode.ReplaceAll, true);
            api.World.BlockAccessor.Commit();

            string note = def.ClearFootprint
                ? " Note: ClearFootprint not applied for /place. Use /wgen seafarer setpos + /wgen regen if the structure merged with terrain."
                : "";
            return TextCommandResult.Success($"Placed '{code}' at {pos} ({placed} blocks).{note}");
        }

        private TextCommandResult OnCmdList(TextCommandCallingArgs args)
        {
            if (scfg == null || scfg.Structures.Length == 0)
                return TextCommandResult.Success("No Seafarer structures configured.");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Seafarer structures:");
            foreach (var def in scfg.Structures)
            {
                bool loaded = def.schematicData != null;
                string status = def.StoryStructure
                    ? (storyLocations.ContainsKey(def.Code) ? "determined" : (attemptedCodes.Contains(def.Code) ? "FAILED" : "pending"))
                    : $"scattered (chance={def.Chance})";
                sb.AppendLine($"  {def.Code} — {def.Placement}, {status}{(loaded ? "" : " [NO SCHEMATIC]")}");
            }
            return TextCommandResult.Success(sb.ToString());
        }
    }
}
