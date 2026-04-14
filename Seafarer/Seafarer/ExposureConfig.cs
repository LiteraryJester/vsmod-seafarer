namespace SaltAndSand;

/// <summary>
/// Configuration for the exposure system. Loaded from ExposureConfig.json.
/// </summary>
public class ExposureConfig
{
    /// <summary>Master toggle for the entire exposure system.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Set to false to disable heatstroke while keeping frostbite active.</summary>
    public bool HeatstrokeEnabled { get; set; } = true;

    /// <summary>Set to false to disable frostbite while keeping heatstroke active.</summary>
    public bool FrostbiteEnabled { get; set; } = true;

    /// <summary>Ambient temperature (C) above which heat exposure accumulates when unsheltered.</summary>
    public float HeatThreshold { get; set; } = 33f;

    /// <summary>Ambient temperature (C) below which cold exposure accumulates when unsheltered.</summary>
    public float ColdThreshold { get; set; } = 0f;

    /// <summary>Base exposure gain per game hour when at the threshold boundary. Scales with severity.</summary>
    public float AccumulationRatePerHour { get; set; } = 0.01f;

    /// <summary>Base exposure loss per game hour when sheltered.</summary>
    public float DecayRatePerHour { get; set; } = 0.004f;

    /// <summary>Multiplier on accumulation rate from wind speed (0 = wind ignored).</summary>
    public float WindAccumulationMultiplier { get; set; } = 0.5f;

    /// <summary>How much 1 HP of healing reduces exposure (0–1 range).</summary>
    public float HealingExposureReduction { get; set; } = 0.01f;

    /// <summary>Exposure level (0–1) at which Tier 1 effects begin.</summary>
    public float Tier1Threshold { get; set; } = 0.4f;

    /// <summary>Exposure level (0–1) at which Tier 2 effects begin.</summary>
    public float Tier2Threshold { get; set; } = 0.7f;

    /// <summary>Exposure level (0–1) at which Tier 3 effects begin.</summary>
    public float Tier3Threshold { get; set; } = 1.0f;

    // --- Heatstroke effect values ---

    /// <summary>Walk speed reduction at Heatstroke I (fraction, e.g. 0.1 = 10% slower).</summary>
    public float HeatstrokeT1SpeedPenalty { get; set; } = 0.1f;

    /// <summary>Extra damage taken multiplier at Heatstroke I (fraction added, e.g. 0.15 = 15% more damage).</summary>
    public float HeatstrokeT1DamageTakenIncrease { get; set; } = 0.15f;

    /// <summary>Hunger rate multiplier increase at Heatstroke II (e.g. 1.5 = 50% faster hunger).</summary>
    public float HeatstrokeT2HungerRateMultiplier { get; set; } = 1.5f;

    /// <summary>Satiety effectiveness reduction at Heatstroke II (fraction, e.g. 0.5 = food gives 50% less).</summary>
    public float HeatstrokeT2SatietyPenalty { get; set; } = 0.5f;

    /// <summary>Walk speed reduction at Heatstroke III.</summary>
    public float HeatstrokeT3SpeedPenalty { get; set; } = 0.35f;

    /// <summary>HP damage per 10 real seconds at Heatstroke III.</summary>
    public float HeatstrokeT3DamagePerTick { get; set; } = 0.5f;

    /// <summary>Temporal stability offset applied at Heatstroke III to trigger hallucinations.</summary>
    public double HeatstrokeT3StabilityOffset { get; set; } = -0.5;

    // --- Frostbite effect values ---

    /// <summary>Max health reduction (points) at Frostbite I.</summary>
    public float FrostbiteT1MaxHealthReduction { get; set; } = 3f;

    /// <summary>Max health reduction (points) at Frostbite II.</summary>
    public float FrostbiteT2MaxHealthReduction { get; set; } = 6f;

    /// <summary>Chance per 10s tick to drop held item at Frostbite II (0–1).</summary>
    public float FrostbiteT2ItemDropChance { get; set; } = 0.05f;

    /// <summary>Max health reduction (points) at Frostbite III.</summary>
    public float FrostbiteT3MaxHealthReduction { get; set; } = 10f;

    /// <summary>Chance per 10s tick to drop held item at Frostbite III.</summary>
    public float FrostbiteT3ItemDropChance { get; set; } = 0.15f;

    /// <summary>HP damage per 10 real seconds at Frostbite III.</summary>
    public float FrostbiteT3DamagePerTick { get; set; } = 0.5f;

    /// <summary>Extra thirst rate per tier when Hydrate or Diedrate is installed (e.g. 0.25 = +25% per tier).</summary>
    public float HodThirstMultiplierPerTier { get; set; } = 0.25f;
}
