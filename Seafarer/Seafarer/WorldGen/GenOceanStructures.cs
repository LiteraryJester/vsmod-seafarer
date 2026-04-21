using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Seafarer.WorldGen
{
    public enum EnumOceanPlacement
    {
        Underwater,
        Coastal,
        BuriedUnderwater,
        OceanSurface
    }

    [ProtoContract]
    public class OceanStructureReservation
    {
        [ProtoMember(1)] public int OriginX;
        [ProtoMember(2)] public int OriginY;
        [ProtoMember(3)] public int OriginZ;
        [ProtoMember(4)] public int VariantIndex;
        [ProtoMember(5)] public int RotationIndex;
        [ProtoMember(6)] public int SizeX;
        [ProtoMember(7)] public int SizeY;
        [ProtoMember(8)] public int SizeZ;
        [ProtoMember(9)] public bool StructureRecorded;
        // False until Y has been resolved. For OceanSurface reservations, Y is resolved
        // immediately at reservation time (= seaLevel + OffsetY). For other placement modes,
        // resolution is deferred to the first chunk-gen event for the chunk containing
        // (OriginX, OriginZ) — since terrain height is only available per chunk.
        [ProtoMember(10)] public bool OriginYResolved;
    }

    public class OceanStructureDef
    {
        public string Code;
        public string[] Schematics;
        public EnumOceanPlacement Placement;
        public float Chance;
        public int MinWaterDepth = 0;
        public int MaxWaterDepth = 255;
        public int OffsetY = 0;
        public int MaxCount = 0;
        public bool SuppressTrees = false;
        public bool RandomRotation = true;

        // World-singleton tracking (0 = unlimited)
        public int GlobalMaxCount = 0;

        // Radial distance from world spawn in blocks (0 = no constraint)
        public int MinSpawnDist = 0;
        public int MaxSpawnDist = 0;

        // When true, reservation happens at world init (SaveGameLoaded) rather than
        // lazily per-chunk. Placement is always chunk-iterative via PlacePartial.
        public bool StoryStructure = false;
    }

    public class OceanStructuresConfig
    {
        public OceanStructureDef[] Structures = Array.Empty<OceanStructureDef>();
    }

    public class GenOceanStructures : ModSystem
    {
        private ICoreServerAPI sapi;
        private OceanStructuresConfig config;
        private IWorldGenBlockAccessor worldgenBlockAccessor;
        private LCGRandom rand;
        private int chunksize;
        private int regionSize;
        private int regionChunkSize;

        // Key: structure code, Value: array of schematic variants.
        // Each variant is an array of 4 rotations (0, 90, 180, 270).
        // BlockSchematicPartial (not BlockSchematic) so we can call PlacePartial for chunk-scoped writes.
        private Dictionary<string, BlockSchematicPartial[][]> cachedSchematics = new();

        // Both dicts below are accessed from the chunk-gen worker thread and from save hooks.
        // Guard all reads and writes with countsLock.

        // World-global count of placed structures, keyed on def.Code.
        private readonly Dictionary<string, int> globalCounts = new();
        // Pending/placed OceanSurface reservations, keyed on def.Code.
        private readonly Dictionary<string, OceanStructureReservation> reservations = new();
        private readonly object countsLock = new();
        private const string CountsDataKey = "seafarer-ocean-structure-counts";
        private const string ReservationsDataKey = "seafarer-ocean-reservations";

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
        public override double ExecuteOrder() => 0.31;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            api.Event.InitWorldGenerator(InitWorldGen, "standard");
            api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
            api.Event.GetWorldgenBlockAccessor(chunkProvider =>
            {
                worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
            });

            api.Event.SaveGameLoaded += OnSaveGameLoaded;
            api.Event.GameWorldSave += OnGameWorldSave;

            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands.GetOrCreate("ocean")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("place")
                    .WithDescription("Force-place an ocean structure at your position")
                    .RequiresPlayer()
                    .WithArgs(parsers.Word("code"))
                    .HandleWith(OnCmdPlace)
                .EndSubCommand()
                .BeginSubCommand("list")
                    .WithDescription("List all registered ocean structure codes")
                    .HandleWith(OnCmdList)
                .EndSubCommand();
        }

        private void InitWorldGen()
        {
            chunksize = GlobalConstants.ChunkSize;
            regionSize = sapi.WorldManager.RegionSize;
            regionChunkSize = regionSize / chunksize;
            rand = new LCGRandom(sapi.WorldManager.Seed ^ 8415718);

            LoadConfig();
            DetermineOceanStoryStructures();
        }

        private void LoadConfig()
        {
            cachedSchematics.Clear();

            var asset = sapi.Assets.TryGet(new AssetLocation("seafarer", "worldgen/oceanstructures.json"));
            if (asset == null)
            {
                Mod.Logger.Notification("No oceanstructures.json found, ocean structure generation disabled.");
                config = new OceanStructuresConfig();
                return;
            }

            config = asset.ToObject<OceanStructuresConfig>();

            foreach (var def in config.Structures)
            {
                var variants = new List<BlockSchematicPartial[]>();

                foreach (var schematicPath in def.Schematics)
                {
                    var schematicAsset = sapi.Assets.TryGet(
                        new AssetLocation("seafarer", "worldgen/schematics/" + schematicPath + ".json")
                    );
                    if (schematicAsset == null)
                    {
                        Mod.Logger.Warning("Ocean structure '{0}': schematic not found at '{1}', skipping.", def.Code, schematicPath);
                        continue;
                    }

                    var baseSchematic = schematicAsset.ToObject<BlockSchematicPartial>();
                    DedupeFluidIndices(baseSchematic, worldgenBlockAccessor);
                    baseSchematic.Init(worldgenBlockAccessor);

                    var rotations = new BlockSchematicPartial[4];
                    for (int r = 0; r < 4; r++)
                    {
                        var copy = (BlockSchematicPartial)baseSchematic.ClonePacked();
                        copy.TransformWhilePacked(sapi.World, EnumOrigin.BottomCenter, r * 90);
                        DedupeFluidIndices(copy, worldgenBlockAccessor);
                        copy.Init(worldgenBlockAccessor);
                        rotations[r] = copy;
                    }

                    variants.Add(rotations);
                }

                if (variants.Count > 0)
                {
                    cachedSchematics[def.Code] = variants.ToArray();
                }
                else
                {
                    Mod.Logger.Warning("Ocean structure '{0}': no valid schematics found, will not generate.", def.Code);
                }
            }

            Mod.Logger.Notification("Loaded {0} ocean structure definitions with {1} total schematic variants.",
                config.Structures.Length, cachedSchematics.Count);
        }

        /// <summary>
        /// Removes duplicate fluid entries from a schematic's packed Indices/BlockIds arrays.
        /// Some schematics (esp. WorldEdit-saved) end up with two entries at the same (x,y,z)
        /// position that both resolve to fluid blocks, which makes BlockSchematicStructure.Init
        /// throw because FluidBlocksByPos is populated via strict Dictionary.Add. Keeping only
        /// the first occurrence at each fluid position gives us the same last-write-wins semantic
        /// the old BlockSchematic.Place path used.
        /// </summary>
        private void DedupeFluidIndices(BlockSchematicPartial schem, IBlockAccessor accessor)
        {
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
                    // unknown block id; keep as-is, Init will skip it via its own null check
                    keptIndices.Add(idx);
                    keptBlockIds.Add(blockId);
                    continue;
                }

                Block block = accessor.GetBlock(blockCode);
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
                Mod.Logger.Warning("Schematic had {0} duplicate fluid entries at the same position; kept first occurrence at each.", removed);
                schem.Indices = keptIndices;
                schem.BlockIds = keptBlockIds;
            }
        }

        private BlockPos spawnPosCache;

        private BlockPos GetSpawnPosSafe()
        {
            if (spawnPosCache != null) return spawnPosCache;
            try
            {
                var ep = sapi.World.DefaultSpawnPosition;
                if (ep == null) return null;
                spawnPosCache = ep.AsBlockPos;
                return spawnPosCache;
            }
            catch
            {
                return null;
            }
        }

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            if (config.Structures.Length == 0) return;

            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;
            var mapChunk = chunks[0].MapChunk;
            var mapRegion = mapChunk.MapRegion;
            int seaLevel = sapi.World.SeaLevel;

            foreach (var def in config.Structures)
            {
                if (!cachedSchematics.TryGetValue(def.Code, out var variants)) continue;

                // Story structures (any placement mode) + OceanSurface (any story flag) use
                // the reservation + per-chunk PlacePartial path.
                if (def.StoryStructure || def.Placement == EnumOceanPlacement.OceanSurface)
                {
                    HandleOceanSurface(request, def, variants, mapRegion, chunkX, chunkZ);
                    continue;
                }

                HandleLegacyPlacement(request, def, variants, mapChunk, mapRegion, chunkX, chunkZ, seaLevel);
            }
        }

        /// <summary>
        /// Legacy per-chunk placement for Underwater / Coastal / BuriedUnderwater modes.
        /// Unchanged behavior from pre-OceanSurface era.
        /// </summary>
        private void HandleLegacyPlacement(IChunkColumnGenerateRequest request, OceanStructureDef def, BlockSchematicPartial[][] variants,
            IMapChunk mapChunk, IMapRegion mapRegion, int chunkX, int chunkZ, int seaLevel)
        {
            rand.InitPositionSeed(chunkX + def.Code.GetHashCode(), chunkZ);
            float roll = (float)rand.NextInt(10000) / 10000f;
            if (roll > def.Chance) return;

            if (def.GlobalMaxCount > 0)
            {
                lock (countsLock)
                {
                    if (globalCounts.TryGetValue(def.Code, out int placed) && placed >= def.GlobalMaxCount) return;
                }
            }

            if (def.MaxCount > 0 && CountExistingStructures(mapRegion, def.Code) >= def.MaxCount) return;

            int localX = rand.NextInt(chunksize);
            int localZ = rand.NextInt(chunksize);
            int posX = chunkX * chunksize + localX;
            int posZ = chunkZ * chunksize + localZ;

            if (def.MinSpawnDist > 0 || def.MaxSpawnDist > 0)
            {
                var spawnPos = GetSpawnPosSafe();
                if (spawnPos == null) return;
                int dx = posX - spawnPos.X;
                int dz = posZ - spawnPos.Z;
                double dist = Math.Sqrt((double)dx * dx + (double)dz * dz);
                if (def.MinSpawnDist > 0 && dist < def.MinSpawnDist) return;
                if (def.MaxSpawnDist > 0 && dist > def.MaxSpawnDist) return;
            }

            float oceanicity = GetOceanicity(mapRegion, posX, posZ);
            float beachStrength = GetBeachStrength(mapRegion, posX, posZ);

            int terrainHeight = mapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
            int waterDepth = seaLevel - terrainHeight;

            if (!IsValidPlacement(def, oceanicity, beachStrength, waterDepth)) return;

            var variantRotations = variants[rand.NextInt(variants.Length)];
            int rotationIndex = def.RandomRotation ? rand.NextInt(4) : 0;
            var schematic = variantRotations[rotationIndex];

            int posY = CalculatePlacementY(def, terrainHeight, schematic);
            var startPos = new BlockPos(posX, posY, posZ);

            schematic.Place(worldgenBlockAccessor, sapi.World, startPos, EnumReplaceMode.ReplaceAll, true);

            mapRegion.AddGeneratedStructure(new GeneratedStructure()
            {
                Code = def.Code,
                Group = "ocean",
                Location = new Cuboidi(
                    startPos.X, startPos.Y, startPos.Z,
                    startPos.X + schematic.SizeX - 1,
                    startPos.Y + schematic.SizeY - 1,
                    startPos.Z + schematic.SizeZ - 1
                ),
                SuppressTreesAndShrubs = def.SuppressTrees,
                SuppressRivulets = true
            });

            if (def.GlobalMaxCount > 0)
            {
                lock (countsLock)
                {
                    globalCounts.TryGetValue(def.Code, out int placed);
                    globalCounts[def.Code] = placed + 1;
                }
            }
        }

        /// <summary>
        /// OceanSurface handler. Two phases per chunk:
        ///   (a) If the def is NOT a story structure and no reservation exists, try to make one lazily.
        ///       (Story structures reserve at init time in DetermineOceanStoryStructures; we do not
        ///        make lazy reservations for them here.)
        ///   (b) If a reservation exists and its cuboid intersects this chunk, place its slice.
        /// </summary>
        private void HandleOceanSurface(IChunkColumnGenerateRequest request, OceanStructureDef def, BlockSchematicPartial[][] variants,
            IMapRegion mapRegion, int chunkX, int chunkZ)
        {
            OceanStructureReservation existing;
            lock (countsLock)
            {
                reservations.TryGetValue(def.Code, out existing);
            }

            // Phase A: create a lazy reservation for non-story defs only
            if (existing == null && !def.StoryStructure)
            {
                existing = TryReserveOceanSurface(def, variants, mapRegion, chunkX, chunkZ);
            }

            if (existing == null) return;
            PlaceOceanSurfaceSlice(request, def, variants, mapRegion, chunkX, chunkZ, existing);
        }

        /// <summary>
        /// Attempts to pick and reserve a valid OceanSurface location using the current chunk as candidate.
        /// Returns the reservation on success, null otherwise.
        /// </summary>
        private OceanStructureReservation TryReserveOceanSurface(OceanStructureDef def, BlockSchematicPartial[][] variants,
            IMapRegion mapRegion, int chunkX, int chunkZ)
        {
            // Singleton gate
            if (def.GlobalMaxCount > 0)
            {
                lock (countsLock)
                {
                    if (globalCounts.TryGetValue(def.Code, out int placed) && placed >= def.GlobalMaxCount) return null;
                }
            }

            // NOTE: per-region def.MaxCount is intentionally NOT checked for OceanSurface;
            // use def.GlobalMaxCount for world-singleton caps.
            rand.InitPositionSeed(chunkX + def.Code.GetHashCode(), chunkZ);
            float roll = (float)rand.NextInt(10000) / 10000f;
            if (roll > def.Chance) return null;

            int localX = rand.NextInt(chunksize);
            int localZ = rand.NextInt(chunksize);
            int candidateX = chunkX * chunksize + localX;
            int candidateZ = chunkZ * chunksize + localZ;

            if (def.MinSpawnDist > 0 || def.MaxSpawnDist > 0)
            {
                var spawnPos = GetSpawnPosSafe();
                if (spawnPos == null) return null;
                int dx = candidateX - spawnPos.X;
                int dz = candidateZ - spawnPos.Z;
                double dist = Math.Sqrt((double)dx * dx + (double)dz * dz);
                if (def.MinSpawnDist > 0 && dist < def.MinSpawnDist) return null;
                if (def.MaxSpawnDist > 0 && dist > def.MaxSpawnDist) return null;
            }

            int variantIdx = rand.NextInt(variants.Length);
            var variantRotations = variants[variantIdx];
            int rotationIndex = def.RandomRotation ? rand.NextInt(4) : 0;
            var schematic = variantRotations[rotationIndex];

            // Center-based origin: structure centered on candidate position
            int originX = candidateX - schematic.SizeX / 2;
            int originZ = candidateZ - schematic.SizeZ / 2;

            if (!ValidateOceanCoverage(mapRegion, originX, originZ, schematic.SizeX, schematic.SizeZ, def)) return null;

            var reservation = new OceanStructureReservation
            {
                OriginX = originX,
                OriginY = sapi.World.SeaLevel + def.OffsetY,
                OriginZ = originZ,
                VariantIndex = variantIdx,
                RotationIndex = rotationIndex,
                SizeX = schematic.SizeX,
                SizeY = schematic.SizeY,
                SizeZ = schematic.SizeZ,
                StructureRecorded = false
            };

            lock (countsLock)
            {
                // Re-check singleton after validation (another thread may have beaten us)
                if (def.GlobalMaxCount > 0 && globalCounts.TryGetValue(def.Code, out int placed2) && placed2 >= def.GlobalMaxCount) return null;
                reservations[def.Code] = reservation;
                globalCounts[def.Code] = globalCounts.GetValueOrDefault(def.Code) + 1;
            }

            Mod.Logger.Notification("Ocean structure '{0}' reserved at ({1}, {2}, {3})", def.Code, originX, reservation.OriginY, originZ);
            return reservation;
        }

        /// <summary>
        /// Paints the slice of a reserved schematic that falls within the current chunk.
        /// Resolves deferred Y on the first chunk-gen that touches the origin chunk.
        /// First-time placement also records the GeneratedStructure for waypoint discovery.
        /// </summary>
        private void PlaceOceanSurfaceSlice(IChunkColumnGenerateRequest request, OceanStructureDef def, BlockSchematicPartial[][] variants,
            IMapRegion mapRegion, int chunkX, int chunkZ, OceanStructureReservation res)
        {
            // Defensive bounds on variant/rotation indices in case save data is from a different config
            if (res.VariantIndex < 0 || res.VariantIndex >= variants.Length) return;
            var rotations = variants[res.VariantIndex];
            if (res.RotationIndex < 0 || res.RotationIndex >= rotations.Length) return;
            var schematic = rotations[res.RotationIndex];
            int seaLevel = sapi.World.SeaLevel;

            // Resolve Y if still deferred (story structures with non-OceanSurface placement).
            // We resolve from the chunk that contains the origin corner (OriginX, OriginZ).
            if (!res.OriginYResolved)
            {
                int originChunkX = (int)Math.Floor((double)res.OriginX / chunksize);
                int originChunkZ = (int)Math.Floor((double)res.OriginZ / chunksize);
                if (chunkX != originChunkX || chunkZ != originChunkZ)
                {
                    // Not our job — wait for the chunk that owns the origin
                    return;
                }

                // We are the origin chunk. Resolve Y from terrain.
                int localX = res.OriginX - originChunkX * chunksize;
                int localZ = res.OriginZ - originChunkZ * chunksize;
                // Guard against edge cases (shouldn't happen but safety first)
                if (localX < 0 || localX >= chunksize || localZ < 0 || localZ >= chunksize) return;

                var mapChunk = request.Chunks[0].MapChunk;
                int terrainHeight = mapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
                int waterDepth = seaLevel - terrainHeight;

                float oceanicity = GetOceanicity(mapRegion, res.OriginX, res.OriginZ);
                float beachStrength = GetBeachStrength(mapRegion, res.OriginX, res.OriginZ);

                if (!IsValidPlacement(def, oceanicity, beachStrength, waterDepth))
                {
                    // Coarse OceanMap validation passed at reservation time but fine-grained terrain
                    // doesn't satisfy the def. Drop the reservation — structure will not generate.
                    Mod.Logger.Warning("Ocean story structure '{0}': per-chunk terrain failed IsValidPlacement (waterDepth={1}); reservation dropped.",
                        def.Code, waterDepth);
                    lock (countsLock)
                    {
                        reservations.Remove(def.Code);
                        if (def.GlobalMaxCount > 0 && globalCounts.TryGetValue(def.Code, out int n) && n > 0)
                        {
                            globalCounts[def.Code] = n - 1;
                        }
                    }
                    return;
                }

                int resolvedY = CalculatePlacementY(def, terrainHeight, schematic);
                lock (countsLock)
                {
                    res.OriginY = resolvedY;
                    res.OriginYResolved = true;
                }
                Mod.Logger.Notification("Ocean story structure '{0}' Y resolved to {1} (terrain={2}, placement={3})",
                    def.Code, resolvedY, terrainHeight, def.Placement);
            }

            // XZ-footprint intersection with current chunk
            int footprintMinX = res.OriginX;
            int footprintMaxX = res.OriginX + res.SizeX;
            int footprintMinZ = res.OriginZ;
            int footprintMaxZ = res.OriginZ + res.SizeZ;
            int chunkMinX = chunkX * chunksize;
            int chunkMaxX = chunkMinX + chunksize;
            int chunkMinZ = chunkZ * chunksize;
            int chunkMaxZ = chunkMinZ + chunksize;

            if (footprintMaxX <= chunkMinX || footprintMinX >= chunkMaxX) return;
            if (footprintMaxZ <= chunkMinZ || footprintMinZ >= chunkMaxZ) return;

            var startPos = new BlockPos(res.OriginX, res.OriginY, res.OriginZ);
            EnumStructurePlacement placementHint;
            switch (def.Placement)
            {
                case EnumOceanPlacement.Underwater:
                case EnumOceanPlacement.BuriedUnderwater:
                    placementHint = EnumStructurePlacement.Underwater;
                    break;
                case EnumOceanPlacement.Coastal:
                case EnumOceanPlacement.OceanSurface:
                default:
                    placementHint = EnumStructurePlacement.Surface;
                    break;
            }

            schematic.PlacePartial(
                request.Chunks, worldgenBlockAccessor, sapi.World,
                chunkX, chunkZ, startPos,
                EnumReplaceMode.ReplaceAll,
                placementHint,
                replaceMeta: true, resolveImports: true
            );

            // Atomic check-and-set so only the first thread adds the GeneratedStructure record.
            lock (countsLock)
            {
                if (res.StructureRecorded) return;
                res.StructureRecorded = true;
            }
            mapRegion.AddGeneratedStructure(new GeneratedStructure()
            {
                Code = def.Code,
                Group = "ocean",
                Location = new Cuboidi(
                    res.OriginX, res.OriginY, res.OriginZ,
                    res.OriginX + res.SizeX - 1,
                    res.OriginY + res.SizeY - 1,
                    res.OriginZ + res.SizeZ - 1
                ),
                SuppressTreesAndShrubs = def.SuppressTrees,
                SuppressRivulets = true
            });
        }

        private float GetOceanicity(IMapRegion mapRegion, int posX, int posZ)
        {
            if (mapRegion.OceanMap == null || mapRegion.OceanMap.Data.Length == 0) return 0;

            var rlX = (posX / chunksize) % regionChunkSize;
            var rlZ = (posZ / chunksize) % regionChunkSize;
            var oFac = (float)mapRegion.OceanMap.InnerSize / regionChunkSize;

            var oceanUpLeft = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac), (int)(rlZ * oFac));
            var oceanUpRight = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac + oFac), (int)(rlZ * oFac));
            var oceanBotLeft = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac), (int)(rlZ * oFac + oFac));
            var oceanBotRight = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac + oFac), (int)(rlZ * oFac + oFac));

            return GameMath.BiLerp(
                oceanUpLeft, oceanUpRight, oceanBotLeft, oceanBotRight,
                (float)(posX % chunksize) / chunksize,
                (float)(posZ % chunksize) / chunksize
            );
        }

        private float GetBeachStrength(IMapRegion mapRegion, int posX, int posZ)
        {
            if (mapRegion.BeachMap == null || mapRegion.BeachMap.Data.Length == 0) return 0;

            var rlX = (posX / chunksize) % regionChunkSize;
            var rlZ = (posZ / chunksize) % regionChunkSize;
            var bFac = (float)mapRegion.BeachMap.InnerSize / regionChunkSize;

            var beachUpLeft = mapRegion.BeachMap.GetUnpaddedInt((int)(rlX * bFac), (int)(rlZ * bFac));
            var beachUpRight = mapRegion.BeachMap.GetUnpaddedInt((int)(rlX * bFac + bFac), (int)(rlZ * bFac));
            var beachBotLeft = mapRegion.BeachMap.GetUnpaddedInt((int)(rlX * bFac), (int)(rlZ * bFac + bFac));
            var beachBotRight = mapRegion.BeachMap.GetUnpaddedInt((int)(rlX * bFac + bFac), (int)(rlZ * bFac + bFac));

            return GameMath.BiLerp(
                beachUpLeft, beachUpRight, beachBotLeft, beachBotRight,
                (float)(posX % chunksize) / chunksize,
                (float)(posZ % chunksize) / chunksize
            );
        }

        /// <summary>
        /// Validates that a candidate footprint has enough open water for OceanSurface placement.
        /// Moderate rule: center + 4 corners must be ocean; at least 7 of 9 samples ocean overall;
        /// center water depth within def.MinWaterDepth..MaxWaterDepth (if set).
        /// </summary>
        private bool ValidateOceanCoverage(IMapRegion mapRegion, int originX, int originZ, int sizeX, int sizeZ, OceanStructureDef def)
        {
            int cx = originX + sizeX / 2;
            int cz = originZ + sizeZ / 2;

            int[,] samples = new int[9, 2]
            {
                { cx, cz },                              // 0: center
                { originX, originZ },                    // 1-4: corners
                { originX + sizeX, originZ },
                { originX, originZ + sizeZ },
                { originX + sizeX, originZ + sizeZ },
                { cx, originZ },                         // 5-8: edge midpoints
                { cx, originZ + sizeZ },
                { originX, cz },
                { originX + sizeX, cz }
            };

            int oceanSamples = 0;
            bool centerOcean = false;
            bool cornersAllOcean = true;

            for (int i = 0; i < 9; i++)
            {
                float oceanicity = GetOceanicity(mapRegion, samples[i, 0], samples[i, 1]);
                bool isOcean = oceanicity > 0;
                if (isOcean) oceanSamples++;

                if (i == 0) centerOcean = isOcean;
                else if (i <= 4 && !isOcean) cornersAllOcean = false;
            }

            if (!centerOcean || !cornersAllOcean) return false;
            if (oceanSamples < 7) return false;

            // Center water-depth check uses the current map chunk's heightmap, which may not be
            // available if the center is in a neighbor chunk. Accept as valid in that case;
            // OceanMap sampling already gave us coarse confidence.
            int seaLevel = sapi.World.SeaLevel;
            var centerMapChunk = sapi.WorldManager.GetMapChunk(cx / chunksize, cz / chunksize);
            if (centerMapChunk == null) return true;

            int terrainHeight = centerMapChunk.WorldGenTerrainHeightMap[(cz % chunksize) * chunksize + (cx % chunksize)];
            int waterDepth = seaLevel - terrainHeight;
            if (def.MinWaterDepth > 0 && waterDepth < def.MinWaterDepth) return false;
            if (def.MaxWaterDepth > 0 && waterDepth > def.MaxWaterDepth) return false;

            return true;
        }

        private bool IsValidPlacement(OceanStructureDef def, float oceanicity, float beachStrength, int waterDepth)
        {
            switch (def.Placement)
            {
                case EnumOceanPlacement.Underwater:
                case EnumOceanPlacement.BuriedUnderwater:
                    return oceanicity > 0 &&
                           waterDepth >= def.MinWaterDepth &&
                           waterDepth <= def.MaxWaterDepth;

                case EnumOceanPlacement.Coastal:
                    bool isBeach = beachStrength > 0;
                    bool isShallowOcean = oceanicity > 0 &&
                                          waterDepth >= 0 &&
                                          waterDepth <= def.MaxWaterDepth;
                    return isBeach || isShallowOcean;

                default:
                    return false;
            }
        }

        private int CalculatePlacementY(OceanStructureDef def, int terrainHeight, BlockSchematic schematic)
        {
            switch (def.Placement)
            {
                case EnumOceanPlacement.Underwater:
                case EnumOceanPlacement.Coastal:
                    return terrainHeight + def.OffsetY;

                case EnumOceanPlacement.BuriedUnderwater:
                    return terrainHeight - schematic.SizeY + def.OffsetY;

                default:
                    return terrainHeight + def.OffsetY;
            }
        }

        private int CountExistingStructures(IMapRegion mapRegion, string code)
        {
            int count = 0;
            foreach (var gs in mapRegion.GeneratedStructures)
            {
                if (gs.Code == code) count++;
            }
            return count;
        }

        private void OnSaveGameLoaded()
        {
            byte[] countsData = sapi.WorldManager.SaveGame.GetData(CountsDataKey);
            byte[] resData = sapi.WorldManager.SaveGame.GetData(ReservationsDataKey);
            lock (countsLock)
            {
                globalCounts.Clear();
                if (countsData != null)
                {
                    var loaded = SerializerUtil.Deserialize<Dictionary<string, int>>(countsData);
                    if (loaded != null)
                    {
                        foreach (var kv in loaded) globalCounts[kv.Key] = kv.Value;
                    }
                }

                reservations.Clear();
                if (resData != null)
                {
                    var loaded = SerializerUtil.Deserialize<Dictionary<string, OceanStructureReservation>>(resData);
                    if (loaded != null)
                    {
                        foreach (var kv in loaded) reservations[kv.Key] = kv.Value;
                    }
                }
            }

        }

        private void OnGameWorldSave()
        {
            Dictionary<string, int> countsSnapshot;
            Dictionary<string, OceanStructureReservation> resSnapshot;
            lock (countsLock)
            {
                countsSnapshot = new Dictionary<string, int>(globalCounts);
                resSnapshot = new Dictionary<string, OceanStructureReservation>(reservations);
            }
            sapi.WorldManager.SaveGame.StoreData(CountsDataKey, SerializerUtil.Serialize(countsSnapshot));
            sapi.WorldManager.SaveGame.StoreData(ReservationsDataKey, SerializerUtil.Serialize(resSnapshot));
        }

        private const int MaxReservationAttempts = 30;

        /// <summary>
        /// Init-time reservation scan for structures flagged StoryStructure = true.
        /// Called from OnSaveGameLoaded after savegame dicts are deserialized.
        /// For each story def without an existing reservation, try up to
        /// MaxReservationAttempts candidate positions in the spawn-distance band.
        /// Map regions are force-loaded as needed.
        /// </summary>
        private void DetermineOceanStoryStructures()
        {
            if (config == null || config.Structures.Length == 0) return;

            // Match base-game GenStructures spawn-lookup chain (GenStructures.cs:147-154):
            // 1) SaveGame.DefaultSpawn — populated from save data, reliable on reloads
            // 2) map center — fresh worlds where spawn hasn't been determined yet
            BlockPos spawnPos;
            var df = sapi.WorldManager.SaveGame.DefaultSpawn;
            if (df != null)
            {
                spawnPos = new BlockPos(df.x, df.y ?? 0, df.z, 0);
            }
            else
            {
                spawnPos = sapi.World.BlockAccessor.MapSize.AsBlockPos / 2;
                Mod.Logger.Notification("Ocean story structures: using map center as spawn reference (fresh world, no saved spawn yet).");
            }

            int seaLevel = sapi.World.SeaLevel;

            foreach (var def in config.Structures)
            {
                if (!def.StoryStructure) continue;
                if (!cachedSchematics.TryGetValue(def.Code, out var variants)) continue;

                // Already reserved (from previous load or a prior save in this session)
                bool alreadyReserved;
                lock (countsLock) { alreadyReserved = reservations.ContainsKey(def.Code); }
                if (alreadyReserved) continue;

                // Respect GlobalMaxCount — if already at cap (e.g., admin-placed), skip
                if (def.GlobalMaxCount > 0)
                {
                    lock (countsLock)
                    {
                        if (globalCounts.TryGetValue(def.Code, out int placed) && placed >= def.GlobalMaxCount) continue;
                    }
                }

                bool reserved = TryReserveStoryLocation(def, variants, spawnPos, seaLevel);
                if (!reserved)
                {
                    Mod.Logger.Warning("Ocean story structure '{0}': failed to find a valid spot after {1} attempts; will not generate this world.", def.Code, MaxReservationAttempts);
                }
            }
        }

        /// <summary>
        /// Try up to MaxReservationAttempts candidate positions for one story structure.
        /// Returns true if a reservation was written.
        /// </summary>
        private bool TryReserveStoryLocation(OceanStructureDef def, BlockSchematicPartial[][] variants, BlockPos spawnPos, int seaLevel)
        {
            // Deterministic-per-world RNG seeded by code + world seed so repeated loads
            // of the same world tend to converge on the same spot if the first attempts
            // happen to all miss initially (edge case; reservations are normally persisted).
            var localRand = new LCGRandom(sapi.WorldManager.Seed ^ (uint)def.Code.GetHashCode());

            int minDist = Math.Max(def.MinSpawnDist, 0);
            int maxDist = def.MaxSpawnDist > 0 ? def.MaxSpawnDist : 5000;   // reasonable default if unset

            for (int attempt = 0; attempt < MaxReservationAttempts; attempt++)
            {
                // Polar sample in the band
                double angle = localRand.NextDouble() * Math.PI * 2.0;
                double radius = minDist + localRand.NextDouble() * Math.Max(1, maxDist - minDist);
                int candidateX = spawnPos.X + (int)(Math.Cos(angle) * radius);
                int candidateZ = spawnPos.Z + (int)(Math.Sin(angle) * radius);

                // Pick variant + rotation
                int variantIdx = localRand.NextInt(variants.Length);
                var rotations = variants[variantIdx];
                int rotationIdx = def.RandomRotation ? localRand.NextInt(4) : 0;
                var schematic = rotations[rotationIdx];

                // Center-based origin
                int originX = candidateX - schematic.SizeX / 2;
                int originZ = candidateZ - schematic.SizeZ / 2;

                // Force-load the map region containing the candidate center
                int rx = (int)Math.Floor((double)candidateX / regionSize);
                int rz = (int)Math.Floor((double)candidateZ / regionSize);
                if (!sapi.WorldManager.BlockingTestMapRegionExists(rx, rz))
                {
                    // Region failed to load (shouldn't happen under normal circumstances)
                    continue;
                }
                var mapRegion = sapi.World.BlockAccessor.GetMapRegion(rx, rz);
                if (mapRegion == null) continue;

                if (!ValidateOceanCoverage(mapRegion, originX, originZ, schematic.SizeX, schematic.SizeZ, def)) continue;

                // Success — build reservation
                bool isOceanSurface = def.Placement == EnumOceanPlacement.OceanSurface;
                var reservation = new OceanStructureReservation
                {
                    OriginX = originX,
                    OriginY = isOceanSurface ? (seaLevel + def.OffsetY) : 0,
                    OriginZ = originZ,
                    VariantIndex = variantIdx,
                    RotationIndex = rotationIdx,
                    SizeX = schematic.SizeX,
                    SizeY = schematic.SizeY,
                    SizeZ = schematic.SizeZ,
                    StructureRecorded = false,
                    OriginYResolved = isOceanSurface
                };

                lock (countsLock)
                {
                    reservations[def.Code] = reservation;
                    globalCounts[def.Code] = globalCounts.GetValueOrDefault(def.Code) + 1;
                }

                Mod.Logger.Notification("Ocean story structure '{0}' reserved at ({1}, {2}, {3}) [Y {4}resolved]",
                    def.Code, originX, reservation.OriginY, originZ,
                    isOceanSurface ? "" : "un");

                return true;
            }

            return false;
        }

        private TextCommandResult OnCmdPlace(TextCommandCallingArgs args)
        {
            var code = (string)args[0];
            var player = args.Caller.Player as IServerPlayer;

            if (!cachedSchematics.TryGetValue(code, out var variants))
            {
                return TextCommandResult.Error("Unknown ocean structure code: " + code);
            }

            var pos = args.Caller.Player.CurrentBlockSelection?.Position
                      ?? player.Entity.Pos.AsBlockPos;

            var variantRotations = variants[sapi.World.Rand.Next(variants.Length)];
            var schematic = variantRotations[sapi.World.Rand.Next(4)];

            int placed = schematic.Place(sapi.World.BlockAccessor, sapi.World, pos, EnumReplaceMode.ReplaceAll, true);
            sapi.World.BlockAccessor.Commit();

            var mapChunk = sapi.WorldManager.GetMapChunk(pos.X / chunksize, pos.Z / chunksize);
            if (mapChunk != null)
            {
                mapChunk.MapRegion.AddGeneratedStructure(new GeneratedStructure()
                {
                    Code = code,
                    Group = "ocean",
                    Location = new Cuboidi(
                        pos.X, pos.Y, pos.Z,
                        pos.X + schematic.SizeX - 1,
                        pos.Y + schematic.SizeY - 1,
                        pos.Z + schematic.SizeZ - 1
                    ),
                    SuppressTreesAndShrubs = true,
                    SuppressRivulets = true
                });
            }

            return TextCommandResult.Success(string.Format("Placed '{0}' at {1} ({2} blocks)", code, pos, placed));
        }

        private TextCommandResult OnCmdList(TextCommandCallingArgs args)
        {
            if (config.Structures.Length == 0)
            {
                return TextCommandResult.Success("No ocean structures configured.");
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Ocean structures:");
            foreach (var def in config.Structures)
            {
                bool hasSchematics = cachedSchematics.ContainsKey(def.Code);
                sb.AppendLine(string.Format("  {0} — {1}, chance={2}, schematics={3}{4}",
                    def.Code, def.Placement, def.Chance,
                    def.Schematics.Length,
                    hasSchematics ? "" : " [NO VALID SCHEMATICS]"));
            }
            return TextCommandResult.Success(sb.ToString());
        }
    }
}
