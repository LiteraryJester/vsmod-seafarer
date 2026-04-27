using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Seafarer;

public class EntityBehaviorExposure : EntityBehavior
{
    private ICoreAPI api = null!;
    private ITreeAttribute expTree = null!;

    private float accum;        // 1-second tick for climate reads
    private float damageAccum;  // 10-second tick for periodic effects

    private BlockPos plrpos = new BlockPos(0);
    private bool hodInstalled;

    private ExposureConfig Config => SeafarerModSystem.ExposureConfig;

    // --- Persisted state via WatchedAttributes ---

    public float ExposureLevel
    {
        get => expTree.GetFloat("level");
        set
        {
            expTree.SetFloat("level", GameMath.Clamp(value, 0f, 1f));
            entity.WatchedAttributes.MarkPathDirty("exposure");
        }
    }

    public ExposureCondition ActiveCondition
    {
        get => (ExposureCondition)expTree.GetInt("condition");
        set
        {
            expTree.SetInt("condition", (int)value);
            entity.WatchedAttributes.MarkPathDirty("exposure");
        }
    }

    public double LastUpdateTotalHours
    {
        get => expTree.GetDouble("lastUpdateHours");
        set
        {
            expTree.SetDouble("lastUpdateHours", value);
            entity.WatchedAttributes.MarkPathDirty("exposure");
        }
    }

    public int ActiveTier
    {
        get => expTree.GetInt("tier");
        set
        {
            expTree.SetInt("tier", value);
            entity.WatchedAttributes.MarkPathDirty("exposure");
        }
    }

    public EntityBehaviorExposure(Entity entity) : base(entity) { }

    public override string PropertyName() => "seafarer:exposure";

    public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
    {
        api = entity.World.Api;

        // Follow the base game pattern (BehaviorBodyTemperature):
        // GetTreeAttribute returns the persisted tree if it exists, null if new entity.
        expTree = entity.WatchedAttributes.GetTreeAttribute("exposure");

        if (expTree == null)
        {
            entity.WatchedAttributes.SetAttribute("exposure", expTree = new TreeAttribute());
            LastUpdateTotalHours = api.World.Calendar.TotalHours;
        }

        hodInstalled = api.ModLoader.IsModEnabled("hydrateordiedrate");
    }

    public override void OnGameTick(float deltaTime)
    {
        if (!Config.Enabled) return;

        accum += deltaTime;
        damageAccum += deltaTime;

        // Climate read and accumulation every 1 second (server only)
        if (accum > 1 && api.Side == EnumAppSide.Server)
        {
            UpdateExposure();
            accum = 0;
        }

        // Periodic effects (item drops, damage) every 10 seconds (server only)
        if (damageAccum > 10 && api.Side == EnumAppSide.Server)
        {
            ApplyPeriodicEffects();
            damageAccum = 0;
        }
    }

    private void UpdateExposure()
    {
        var eplr = entity as EntityPlayer;
        var plr = eplr?.Player;
        if (plr == null) return;
        if (plr.WorldData.CurrentGameMode == EnumGameMode.Creative ||
            plr.WorldData.CurrentGameMode == EnumGameMode.Spectator)
        {
            ClearAllEffects();
            return;
        }

        plrpos.Set((int)entity.Pos.X, (int)(entity.Pos.Y + entity.LocalEyePos.Y * 0.5), (int)entity.Pos.Z);
        plrpos.SetDimension(entity.Pos.Dimension);

        double currentHours = api.World.Calendar.TotalHours;
        float hoursPassed = (float)(currentHours - LastUpdateTotalHours);

        // Guard against time going backwards (world reload, backup restore)
        if (hoursPassed < 0)
        {
            LastUpdateTotalHours = currentHours;
            return;
        }
        if (hoursPassed < 0.001f) return;
        // Cap to prevent huge jumps after loading/reconnecting — max 1 game hour per tick
        if (hoursPassed > 1f) hoursPassed = 1f;
        LastUpdateTotalHours = currentHours;

        var conds = api.World.BlockAccessor.GetClimateAt(plrpos, EnumGetClimateMode.NowValues);
        if (conds == null) return;

        float temp = conds.Temperature;
        var windVec = api.World.BlockAccessor.GetWindSpeedAt(plrpos);
        float windSpeed = (float)windVec.Length();
        float windMultiplier = 1f + windSpeed * Config.WindAccumulationMultiplier;

        float exposureChange;

        if (Config.HeatstrokeEnabled && temp >= Config.HeatThreshold)
        {
            float severity = (temp - Config.HeatThreshold) / 10f;
            exposureChange = Config.AccumulationRatePerHour * (1f + severity) * windMultiplier * hoursPassed;
            ActiveCondition = ExposureCondition.Heatstroke;
        }
        else if (Config.FrostbiteEnabled && temp <= Config.ColdThreshold)
        {
            float severity = (Config.ColdThreshold - temp) / 10f;
            exposureChange = Config.AccumulationRatePerHour * (1f + severity) * windMultiplier * hoursPassed;
            ActiveCondition = ExposureCondition.Frostbite;
        }
        else
        {
            exposureChange = -Config.DecayRatePerHour * hoursPassed;
        }

        float newLevel = GameMath.Clamp(ExposureLevel + exposureChange, 0f, 1f);

        if (exposureChange != 0 && damageAccum > 9.5f)
        {
            api.Logger.Debug(
                "[Exposure] temp={0:F1} frostEn={1} coldThresh={2} heatThresh={3} change={4:F6} level={5:F4}→{6:F4} cond={7}",
                temp, Config.FrostbiteEnabled, Config.ColdThreshold, Config.HeatThreshold,
                exposureChange, ExposureLevel, newLevel, ActiveCondition);
        }

        ExposureLevel = newLevel;

        if (newLevel <= 0f)
        {
            ActiveCondition = ExposureCondition.None;
        }

        UpdateTierEffects();
    }

    /// <summary>
    /// Tracks the condition type that effects were last applied for,
    /// so we can detect condition switches at the same tier level.
    /// </summary>
    private ExposureCondition lastAppliedCondition = ExposureCondition.None;

    private void UpdateTierEffects()
    {
        float level = ExposureLevel;
        int newTier;
        if (level >= Config.Tier3Threshold) newTier = 3;
        else if (level >= Config.Tier2Threshold) newTier = 2;
        else if (level >= Config.Tier1Threshold) newTier = 1;
        else newTier = 0;

        int oldTier = ActiveTier;
        var currentCondition = ActiveCondition;
        bool conditionChanged = currentCondition != lastAppliedCondition && newTier > 0;

        if (newTier != oldTier || conditionChanged)
        {
            RemoveTierEffects(oldTier);
            ApplyTierEffects(newTier);
            ActiveTier = newTier;
            lastAppliedCondition = currentCondition;
        }
    }

    private void ApplyTierEffects(int tier)
    {
        if (tier <= 0)
        {
            RemoveAllStatModifiers();
            return;
        }

        var condition = ActiveCondition;

        if (condition == ExposureCondition.Heatstroke)
        {
            ApplyHeatstrokeEffects(tier);
        }
        else if (condition == ExposureCondition.Frostbite)
        {
            ApplyFrostbiteEffects(tier);
        }
    }

    private void ApplyHeatstrokeEffects(int tier)
    {
        RemoveAllStatModifiers();

        // Each tier applies its own specific values — no cascading
        switch (tier)
        {
            case 1:
                entity.Stats.Set("walkspeed", "exposurePenalty", -Config.HeatstrokeT1SpeedPenalty);
                entity.Stats.Set("healingeffectivness", "exposurePenalty", -Config.HeatstrokeT1DamageTakenIncrease);
                break;
            case 2:
                entity.Stats.Set("walkspeed", "exposurePenalty", -Config.HeatstrokeT1SpeedPenalty);
                entity.Stats.Set("healingeffectivness", "exposurePenalty", -Config.HeatstrokeT1DamageTakenIncrease);
                entity.Stats.Set("hungerrate", "exposurePenalty", Config.HeatstrokeT2HungerRateMultiplier - 1f);
                break;
            case 3:
                entity.Stats.Set("walkspeed", "exposurePenalty", -Config.HeatstrokeT3SpeedPenalty);
                entity.Stats.Set("healingeffectivness", "exposurePenalty", -Config.HeatstrokeT1DamageTakenIncrease);
                entity.Stats.Set("hungerrate", "exposurePenalty", Config.HeatstrokeT2HungerRateMultiplier - 1f);
                var stabBehavior = entity.GetBehavior<Vintagestory.GameContent.EntityBehaviorTemporalStabilityAffected>();
                if (stabBehavior != null)
                {
                    stabBehavior.OwnStability = Math.Max(0, stabBehavior.OwnStability + Config.HeatstrokeT3StabilityOffset);
                }
                break;
        }

        if (hodInstalled && tier >= 1)
        {
            entity.Stats.Set("thirstRateMul", "exposurePenalty", tier * Config.HodThirstMultiplierPerTier);
        }
    }

    private void ApplyFrostbiteEffects(int tier)
    {
        RemoveAllStatModifiers();

        switch (tier)
        {
            case 1:
                entity.Stats.Set("maxhealthExtraPoints", "exposurePenalty", -Config.FrostbiteT1MaxHealthReduction);
                break;
            case 2:
                entity.Stats.Set("maxhealthExtraPoints", "exposurePenalty", -Config.FrostbiteT2MaxHealthReduction);
                break;
            case 3:
                entity.Stats.Set("maxhealthExtraPoints", "exposurePenalty", -Config.FrostbiteT3MaxHealthReduction);
                break;
        }
    }

    private void RemoveTierEffects(int tier)
    {
        RemoveAllStatModifiers();

        if (tier >= 3 && ActiveCondition == ExposureCondition.Heatstroke)
        {
            var stabBehavior = entity.GetBehavior<Vintagestory.GameContent.EntityBehaviorTemporalStabilityAffected>();
            if (stabBehavior != null)
            {
                stabBehavior.OwnStability = Math.Min(1.0, stabBehavior.OwnStability - Config.HeatstrokeT3StabilityOffset);
            }
        }
    }

    private void RemoveAllStatModifiers()
    {
        entity.Stats.Remove("walkspeed", "exposurePenalty");
        entity.Stats.Remove("healingeffectivness", "exposurePenalty");
        entity.Stats.Remove("hungerrate", "exposurePenalty");
        entity.Stats.Remove("maxhealthExtraPoints", "exposurePenalty");
        entity.Stats.Remove("thirstRateMul", "exposurePenalty");
    }

    /// <summary>
    /// Handles Frostbite II item drops and Tier III damage/effects on a 10-second cycle.
    /// </summary>
    private void ApplyPeriodicEffects()
    {
        if (ActiveTier < 3) return;

        var condition = ActiveCondition;

        if (condition == ExposureCondition.Heatstroke)
        {
            entity.ReceiveDamage(
                new DamageSource { DamageTier = 0, Source = EnumDamageSource.Weather, Type = EnumDamageType.Heat },
                Config.HeatstrokeT3DamagePerTick
            );
        }
        else if (condition == ExposureCondition.Frostbite)
        {
            entity.ReceiveDamage(
                new DamageSource { DamageTier = 0, Source = EnumDamageSource.Weather, Type = EnumDamageType.Frost },
                Config.FrostbiteT3DamagePerTick
            );
        }
    }

    /// <summary>
    /// Called when the entity receives damage or healing. Healing items use
    /// EnumDamageType.Heal with a positive damage value (see CollectibleBehaviorHealingItem).
    /// We intercept heal events to reduce exposure proportionally.
    /// </summary>
    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
        if (damageSource.Type == EnumDamageType.Heal && Config.Enabled && ExposureLevel > 0)
        {
            float healAmount = damage;
            float reduction = healAmount * Config.HealingExposureReduction;
            ExposureLevel = Math.Max(0, ExposureLevel - reduction);
            UpdateTierEffects();
        }
    }

    private void ClearAllEffects()
    {
        if (ExposureLevel > 0 || ActiveTier > 0)
        {
            RemoveTierEffects(ActiveTier);
            ExposureLevel = 0f;
            ActiveCondition = ExposureCondition.None;
            ActiveTier = 0;
        }
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        RemoveAllStatModifiers();
    }
}
