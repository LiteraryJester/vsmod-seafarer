using Vintagestory.API.Common;

namespace Seafarer;

public class BlockGriddle : Block
{
    public string GetMaterial()
    {
        return Variant["material"] ?? "clay";
    }

    public float GetCookSpeedMultiplier()
    {
        return SeafarerModSystem.GriddleConfig.GetCookSpeedMultiplier(GetMaterial());
    }

    public bool GetSupportsOil()
    {
        return Attributes?["supportsOil"].AsBool(false) ?? false;
    }
}
