using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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
            // Placeholder — implemented in Task 3
        }
    }
}
