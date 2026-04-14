using System.Collections.Generic;

namespace SaltAndSand;

public class GriddleConfig
{
    public Dictionary<string, float> CookSpeedMultipliers { get; set; } = new()
    {
        ["clay"] = 1.0f,
        ["copper"] = 1.5f,
        ["bronze"] = 1.75f,
        ["tinbronze"] = 1.75f,
        ["bismuthbronze"] = 1.75f,
        ["blackbronze"] = 1.75f,
        ["iron"] = 2.0f,
        ["steel"] = 2.0f
    };

    public int CookingTickIntervalMs { get; set; } = 500;

    public float GetCookSpeedMultiplier(string material)
    {
        return CookSpeedMultipliers.TryGetValue(material, out float mult) ? mult : 1.0f;
    }
}
