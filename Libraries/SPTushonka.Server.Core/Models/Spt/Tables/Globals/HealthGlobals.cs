using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Spt.Tables.Globals;

public record HealthGlobals
{
    public required HealthEffects Effects { get; set; }

    public required FallingSettings Falling { get; set; }

    public required HealPriceSettings HealPrice { get; set; }

    public required ProfileHealthSettings ProfileHealthSettings { get; set; }
}

public record HealthEffects
{
    public required TimedEffectSettings Berserk { get; set; }

    public required BodyTemperatureSettings BodyTemperature { get; set; }

    public required InjuryEffectSettings BreakPart { get; set; }

    public required ChronicStaminaFatigueSettings ChronicStaminaFatigue { get; set; }

    public required DummyEffectSettings Contusion { get; set; }

    public required DehydrationSettings Dehydration { get; set; }

    public required DummyEffectSettings Disorientation { get; set; }

    public required ExhaustionSettings Exhaustion { get; set; }

    public required ExistenceSettings Existence { get; set; }

    public required DummyEffectSettings Flash { get; set; }

    public required InjuryEffectSettings Fracture { get; set; }

    public required BleedingEffectSettings HeavyBleeding { get; set; }

    public required IntoxicationSettings Intoxication { get; set; }

    public required BleedingEffectSettings LightBleeding { get; set; }

    public required LowEdgeHealthSettings LowEdgeHealth { get; set; }

    public required MedEffectSettings MedEffect { get; set; }

    public required MusclePainSettings MildMusclePain { get; set; }

    public required PainSettings Pain { get; set; }

    public required DummyEffectSettings PainKiller { get; set; }

    public required DamageLoopEffectSettings RadExposure { get; set; }

    public required RegenerationSettings Regeneration { get; set; }

    public required DummyEffectSettings SandingScreen { get; set; }

    public required MusclePainSettings SevereMusclePain { get; set; }

    public required StimulatorSettings Stimulator { get; set; }

    public required DummyEffectSettings Stun { get; set; }

    public required TearGasSettings TearGasStrong { get; set; }

    public required TearGasSettings TearGasWeak { get; set; }

    public required TremorSettings Tremor { get; set; }

    public required WoundSettings Wound { get; set; }

    public required ZombieInfectionSettings ZombieInfection { get; set; }
}

public record TimedEffectSettings
{
    public required double DefaultDelay { get; set; }

    public required double DefaultResidueTime { get; set; }

    public required double WorkingTime { get; set; }
}

public record BodyTemperatureSettings
{
    public required double DefaultBuildUpTime { get; set; }

    public required double DefaultResidueTime { get; set; }

    public required double LoopTime { get; set; }
}

public record InjuryEffectSettings
{
    public required ProbabilitySettings BulletHitProbability { get; set; }

    public required double DefaultDelay { get; set; }

    public required double DefaultResidueTime { get; set; }

    public required ProbabilitySettings FallingProbability { get; set; }

    public required double HealExperience { get; set; }

    public required double OfflineDurationMax { get; set; }

    public required double OfflineDurationMin { get; set; }

    public required double RemovePrice { get; set; }

    public required bool RemovedAfterDeath { get; set; }
}

public record ProbabilitySettings
{
    public required double B { get; set; }

    public required string FunctionType { get; set; }

    public required double K { get; set; }

    public required double Threshold { get; set; }
}

public record DummyEffectSettings
{
    public required double Dummy { get; set; }
}

public record ChronicStaminaFatigueSettings
{
    public required double EnergyRate { get; set; }

    public required double EnergyRatePerStack { get; set; }

    public required double TicksEvery { get; set; }

    public required double WorkingTime { get; set; }
}

public record DehydrationSettings
{
    public required double BleedingHealth { get; set; }

    public required double BleedingLifeTime { get; set; }

    public required double BleedingLoopTime { get; set; }

    public required double DamageOnStrongDehydration { get; set; }

    public required double DefaultDelay { get; set; }

    public required double DefaultResidueTime { get; set; }

    public required double StrongDehydrationLoopTime { get; set; }
}

public record ExhaustionSettings
{
    public required double Damage { get; set; }

    public required double DamageLoopTime { get; set; }

    public required double DefaultDelay { get; set; }

    public required double DefaultResidueTime { get; set; }
}

public record ExistenceSettings
{
    public required double DestroyedStomachEnergyTimeFactor { get; set; }

    public required double DestroyedStomachHydrationTimeFactor { get; set; }

    public required double EnergyDamage { get; set; }

    public required double EnergyLoopTime { get; set; }

    public required double HydrationDamage { get; set; }

    public required double HydrationLoopTime { get; set; }
}

public record BleedingEffectSettings
{
    public required double DamageEnergy { get; set; }

    public required double DamageHealth { get; set; }

    public required double DamageHealthDehydrated { get; set; }

    public required double DefaultDelay { get; set; }

    public required double DefaultResidueTime { get; set; }

    public required double EliteVitalityDuration { get; set; }

    public required double EnergyLoopTime { get; set; }

    public required double HealExperience { get; set; }

    public required double HealthLoopTime { get; set; }

    public required double HealthLoopTimeDehydrated { get; set; }

    public required double LifeTimeDehydrated { get; set; }

    public required double OfflineDurationMax { get; set; }

    public required double OfflineDurationMin { get; set; }

    public required ProbabilitySettings Probability { get; set; }

    public required double RemovePrice { get; set; }

    public required bool RemovedAfterDeath { get; set; }
}

public record IntoxicationSettings
{
    public required double DamageHealth { get; set; }

    public required double DefaultDelay { get; set; }

    public required double DefaultResidueTime { get; set; }

    public required double HealExperience { get; set; }

    public required double HealthLoopTime { get; set; }

    public required double OfflineDurationMax { get; set; }

    public required double OfflineDurationMin { get; set; }

    public required double RemovePrice { get; set; }

    public required bool RemovedAfterDeath { get; set; }
}

public record LowEdgeHealthSettings
{
    public required double DefaultDelay { get; set; }

    public required double DefaultResidueTime { get; set; }

    public required double StartCommonHealth { get; set; }
}

public record MedEffectSettings
{
    public required double DrinkStartDelay { get; set; }

    public required double DrugsStartDelay { get; set; }

    public required double FoodStartDelay { get; set; }

    public required double LoopTime { get; set; }

    public required double MedKitStartDelay { get; set; }

    public required double MedicalStartDelay { get; set; }

    public required double StartDelay { get; set; }

    public required double StimulatorStartDelay { get; set; }
}

public record MusclePainSettings
{
    public required double GymEffectivity { get; set; }

    public required double OfflineDurationMax { get; set; }

    public required double OfflineDurationMin { get; set; }

    public required double TraumaChance { get; set; }
}

public record PainSettings
{
    public required double HealExperience { get; set; }

    public required double TremorDelay { get; set; }
}

public record DamageLoopEffectSettings
{
    public required double Damage { get; set; }

    public required double DamageLoopTime { get; set; }
}

public record RegenerationSettings
{
    public required Dictionary<string, BodyHealthValue> BodyHealth { get; set; }

    public required double Energy { get; set; }

    public required double Hydration { get; set; }

    public required Dictionary<string, RegenerationInfluence> Influences { get; set; }

    public required double LoopTime { get; set; }

    public required double MinimumHealthPercentage { get; set; }
}

public record BodyHealthValue
{
    public required double Value { get; set; }
}

public record RegenerationInfluence
{
    public required double EnergySlowDownPercentage { get; set; }

    public required double HealthSlowDownPercentage { get; set; }

    public required double HydrationSlowDownPercentage { get; set; }
}

public record StimulatorSettings
{
    public required double BuffLoopTime { get; set; }

    public required Dictionary<string, List<StimulatorBuff>> Buffs { get; set; }
}

public record StimulatorBuff
{
    public required bool AbsoluteValue { get; set; }

    public List<string> AppliesTo { get; set; } = [];

    public required string BuffType { get; set; }

    public required double Chance { get; set; }

    public required double Delay { get; set; }

    public required double Duration { get; set; }

    public required string SkillName { get; set; }

    public required double Value { get; set; }
}

public record TearGasSettings
{
    public required List<TearGasBlockingItem> BlockingItems { get; set; }

    public required bool CancelHealingAndInteractions { get; set; }

    public required double ChestDamagePerTick { get; set; }

    public required double CoughCooldown { get; set; }

    public required double ErgonomicsPenalty { get; set; }

    public required double StaminaRateDebuff { get; set; }

    public required double TickTime { get; set; }

    public required double WorkTime { get; set; }
}

public record TearGasBlockingItem
{
    public required string ItemId { get; set; }
}

public record TremorSettings
{
    public required double DefaultDelay { get; set; }

    public required double DefaultResidueTime { get; set; }
}

public record WoundSettings
{
    public required double ThresholdMax { get; set; }

    public required double ThresholdMin { get; set; }

    public required double WorkingTime { get; set; }
}

public record ZombieInfectionSettings
{
    public required double Dehydration { get; set; }

    public required double HearingDebuffPercentage { get; set; }

    // The C on the Cumulatie down here is the russian C, its encoded differently, I THINK
    // Just in case, dont change it
    [JsonPropertyName("СumulativeTime")]
    public required double CumulativeTime { get; set; }
}

public record FallingSettings
{
    public required double DamagePerMeter { get; set; }

    public required double SafeHeight { get; set; }
}

public record HealPriceSettings
{
    public required double EnergyPointPrice { get; set; }

    public required double HealthPointPrice { get; set; }

    public required double HydrationPointPrice { get; set; }

    public required double TrialLevels { get; set; }

    public required double TrialRaids { get; set; }
}

public record ProfileHealthSettings
{
    public required BodyPartsSettings BodyPartsSettings { get; set; }

    public required string DefaultStimulatorBuff { get; set; }

    public required HealthFactorsSettings HealthFactorsSettings { get; set; }
}

public record BodyPartsSettings
{
    public required BodyPartHealthSettings Head { get; set; }

    public required BodyPartHealthSettings Chest { get; set; }

    public required BodyPartHealthSettings Stomach { get; set; }

    public required BodyPartHealthSettings LeftArm { get; set; }

    public required BodyPartHealthSettings RightArm { get; set; }

    public required BodyPartHealthSettings LeftLeg { get; set; }

    public required BodyPartHealthSettings RightLeg { get; set; }
}

public record BodyPartHealthSettings
{
    public required double Default { get; set; }

    public required double EnvironmentDamageMultiplier { get; set; }

    public required double Maximum { get; set; }

    public required double Minimum { get; set; }

    public required double OverDamageReceivedMultiplier { get; set; }
}

public record HealthFactorsSettings
{
    public required HealthFactorSettings Energy { get; set; }

    public required HealthFactorSettings Hydration { get; set; }

    public required HealthFactorSettings Temperature { get; set; }

    public required HealthFactorSettings Poisoning { get; set; }

    public required HealthFactorSettings Radiation { get; set; }
}

public record HealthFactorSettings
{
    public required double Default { get; set; }

    public required double Maximum { get; set; }

    public required double Minimum { get; set; }
}
