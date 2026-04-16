using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
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
        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
        public override double ExecuteOrder() => 0.31;
    }
}
