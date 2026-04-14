namespace Seafarer;

/// <summary>
/// The type of environmental condition currently affecting the player.
/// Determined by the most recent exposure source ("last source wins").
/// </summary>
public enum ExposureCondition
{
    None = 0,
    Heatstroke = 1,
    Frostbite = 2
}
