namespace SaltAndSand;

public class DryingFrameConfig
{
    /// <summary>Max perish speed multiplier during heavy rain (1.0 = no effect).</summary>
    public float RainRotMultiplier { get; set; } = 2.0f;

    /// <summary>Wind drying bonus added to DryingSpeedMultiplier (windSpeed * this value). At default 30 with max wind, drying takes ~1 day.</summary>
    public float WindDryMultiplier { get; set; } = 30f;

    /// <summary>Milliseconds between weather checks. Higher values reduce server load.</summary>
    public int WeatherCheckIntervalMs { get; set; } = 3000;

    /// <summary>Rainfall level above which rain rot activates (0–1). Default 0.05 = light rain triggers it.</summary>
    public float RainThreshold { get; set; } = 0.05f;

    /// <summary>Set to false to disable rain-based spoilage acceleration.</summary>
    public bool EnableRainRot { get; set; } = true;

    /// <summary>Set to false to disable wind-based drying acceleration.</summary>
    public bool EnableWindDrying { get; set; } = true;

    /// <summary>Base drying speed multiplier on the rack (items use long base times so they barely dry elsewhere).</summary>
    public float DryingSpeedMultiplier { get; set; } = 10f;
}
