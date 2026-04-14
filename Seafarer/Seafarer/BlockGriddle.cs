using Vintagestory.API.Common;

namespace SaltAndSand;

public class BlockGriddle : Block
{
    public string GetMaterial()
    {
        return Variant["material"] ?? "clay";
    }

    public float GetCookSpeedMultiplier()
    {
        return SaltAndSandModSystem.GriddleConfig.GetCookSpeedMultiplier(GetMaterial());
    }

    public bool GetSupportsOil()
    {
        return Attributes?["supportsOil"].AsBool(false) ?? false;
    }
}
