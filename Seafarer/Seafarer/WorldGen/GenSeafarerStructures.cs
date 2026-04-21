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
}
