namespace Seafarer;

public class SaltPanConfig
{
    /// <summary>Master toggle for the salt pan evaporation system. Set to false to disable evaporation entirely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum litres of saltwater the pan holds.</summary>
    public float CapacityLitres { get; set; } = 10f;

    /// <summary>Base evaporation rate in litres per hour at 20°C (multiplier 1.0).</summary>
    public float BaseEvapRatePerHour { get; set; } = 0.208f;

    /// <summary>Salt items yielded per litre of saltwater evaporated.</summary>
    public float SaltYieldPerLitre { get; set; } = 0.4f;

    /// <summary>Milliseconds between weather/evaporation checks.</summary>
    public int WeatherCheckIntervalMs { get; set; } = 3000;

    /// <summary>Rainfall level above which evaporation stalls (0–1). Default 0.05 = light rain stops it.</summary>
    public float RainThreshold { get; set; } = 0.05f;

    /// <summary>Minimum temperature (°C) for evaporation to occur. Below this, evaporation stalls.</summary>
    public float MinEvaporationTemperature { get; set; } = 0f;

    /// <summary>Temperature (°C) at which the evaporation rate multiplier is 1.0. Higher temps speed up evaporation.</summary>
    public float TemperatureScaleBase { get; set; } = 20f;

    /// <summary>Maximum temperature multiplier cap. At default 2.0, evaporation can be at most 2x the base rate.</summary>
    public float MaxTemperatureMultiplier { get; set; } = 2f;
}
