using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Seafarer.WorldGen
{
    public enum EnumOceanPlacement
    {
        Underwater,
        Coastal,
        BuriedUnderwater
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
        private Dictionary<string, BlockSchematic[][]> cachedSchematics = new();

        // World-global count of placed structures, keyed on def.Code.
        // Accessed from the chunk-gen worker thread and from save hooks, so guard with countsLock.
        private readonly Dictionary<string, int> globalCounts = new();
        private readonly object countsLock = new();
        private const string CountsDataKey = "seafarer-ocean-structure-counts";

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
                var variants = new List<BlockSchematic[]>();

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

                    var baseSchematic = schematicAsset.ToObject<BlockSchematic>();
                    baseSchematic.Init(worldgenBlockAccessor);

                    var rotations = new BlockSchematic[4];
                    for (int r = 0; r < 4; r++)
                    {
                        var copy = baseSchematic.ClonePacked();
                        copy.TransformWhilePacked(sapi.World, EnumOrigin.BottomCenter, r * 90);
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

                rand.InitPositionSeed(chunkX + def.Code.GetHashCode(), chunkZ);
                float roll = (float)rand.NextInt(10000) / 10000f;
                if (roll > def.Chance) continue;

                if (def.MaxCount > 0 && CountExistingStructures(mapRegion, def.Code) >= def.MaxCount) continue;

                int localX = rand.NextInt(chunksize);
                int localZ = rand.NextInt(chunksize);
                int posX = chunkX * chunksize + localX;
                int posZ = chunkZ * chunksize + localZ;

                float oceanicity = GetOceanicity(mapRegion, posX, posZ);
                float beachStrength = GetBeachStrength(mapRegion, posX, posZ);

                int terrainHeight = mapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
                int waterDepth = seaLevel - terrainHeight;

                if (!IsValidPlacement(def, oceanicity, beachStrength, waterDepth)) continue;

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
            }
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
            byte[] data = sapi.WorldManager.SaveGame.GetData(CountsDataKey);
            lock (countsLock)
            {
                globalCounts.Clear();
                if (data != null)
                {
                    var loaded = SerializerUtil.Deserialize<Dictionary<string, int>>(data);
                    if (loaded != null)
                    {
                        foreach (var kv in loaded) globalCounts[kv.Key] = kv.Value;
                    }
                }
            }
        }

        private void OnGameWorldSave()
        {
            Dictionary<string, int> snapshot;
            lock (countsLock)
            {
                snapshot = new Dictionary<string, int>(globalCounts);
            }
            sapi.WorldManager.SaveGame.StoreData(CountsDataKey, SerializerUtil.Serialize(snapshot));
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
