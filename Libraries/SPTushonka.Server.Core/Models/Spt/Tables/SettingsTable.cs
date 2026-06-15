using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace SPTarkov.Server.Core.Models.Spt.Tables;

public record SettingsTable
{
    [JsonPropertyName("config")]
    public required Settings Configuration { get; init; }
}

public record Settings
{
    public required int AdditionalRandomDelaySeconds { get; set; }

    public required int AFKTimeoutSeconds { get; set; }

    public required AudioSettings AudioSettings { get; set; }

    public required int ClientSendRateLimit { get; set; }

    public required bool CollectLoadTimeMetrics { get; set; }

    public required int CriticalRetriesCount { get; set; }

    public required int DefaultRetriesCount { get; set; }

    public required int FirstCycleDelaySeconds { get; set; }

    public required FramerateLimit FramerateLimit { get; set; }

    public required int GroupStatusButtonInterval { get; set; }

    public required int GroupStatusInterval { get; set; }

    public required int KeepAliveInterval { get; set; }

    public required int LobbyConnectionPercentage { get; set; }

    public required int LobbyKeepAliveInterval { get; set; }

    public required bool Mark502and504AsNonImportant { get; set; }

    public required MemoryManagementSettings MemoryManagementSettings { get; set; }

    public required NetworkStateView NetworkStateView { get; set; }

    public required int NextCycleDelaySeconds { get; set; }

    public required List<int> NotifierLobbyAidsForce { get; set; } = [];

    public required int NotifierLobbyPercentage { get; set; }

    public required bool NotifierUseLobby { get; set; }

    public required bool NVidiaHighlights { get; set; }

    public required int PingServerResultSendInterval { get; set; }

    public required int PingServersInterval { get; set; }

    public required ReleaseProfiler ReleaseProfiler { get; set; }

    public required List<double> RequestConfirmationTimeouts { get; set; } = [];

    public required List<string> RequestsMadeThroughLobby { get; set; } = [];

    public required int SecondCycleDelaySeconds { get; set; }

    public required bool ShouldEstablishLobbyConnection { get; set; }

    public required int SteamSyncCooldownSeconds { get; set; }

    public required bool TurnOffLogging { get; set; }

    public required int WeaponOverlapDistanceCulling { get; set; }

    public required bool WebDiagnosticsEnabled { get; set; }

    public required List<int> WsReconnectionDelays { get; set; } = [];
}

public record AudioSettings
{
    public required List<AudioGroupPreset> AudioGroupPresets { get; set; } = [];

    public required EnvironmentSettings EnvironmentSettings { get; set; }

    public required HeadphonesSettings HeadphonesSettings { get; set; }

    public required MasterMixerSettings MasterMixerSettings { get; set; }

    public required MetaXRAudioPluginSettings MetaXRAudioPluginSettings { get; set; }

    public required OcclusionSettings OcclusionSettings { get; set; }

    public required PlayerSettings PlayerSettings { get; set; }

    public required WeaponSettings WeaponSettings { get; set; }
}

public record AudioGroupPreset
{
    public required int AngleToAllowBinaural { get; set; }

    public required bool DisabledBinauralByDistance { get; set; }

    public required int DistanceToAllowBinaural { get; set; }

    public required int GroupType { get; set; }

    public required int HeightToAllowBinaural { get; set; }

    public required string Name { get; set; } = string.Empty;

    public required bool OcclusionEnabled { get; set; }

    public required int OcclusionIntensity { get; set; }

    public required double OcclusionRolloffScale { get; set; }

    public required double OverallVolume { get; set; }
}

public record EnvironmentSettings
{
    public required AutumnLateSettings AutumnLateSettings { get; set; }

    public required AutumnLateSettings AutumnSettings { get; set; }

    public required AutumnLateSettings SpringEarlySettings { get; set; }

    public required AutumnLateSettings SpringSettings { get; set; }

    public required AutumnLateSettings StormSettings { get; set; }

    public required AutumnLateSettings SummerSettings { get; set; }

    public required List<SurfaceMultiplier> SurfaceMultipliers { get; set; } = [];

    public required AutumnLateSettings WinterSettings { get; set; }
}

public record AutumnLateSettings
{
    public required List<RainSettingsItem> RainSettings { get; set; } = [];

    public required double StepsVolumeMultiplier { get; set; }

    public required List<WindMultiplier> WindMultipliers { get; set; } = [];
}

public record RainSettingsItem
{
    public required float IndoorVolumeMult { get; set; }

    public required float OutdoorVolumeMult { get; set; }

    public required string RainIntensity { get; set; } = string.Empty;
}

public record WindMultiplier
{
    public required double VolumeMult { get; set; }

    public required string WindSpeed { get; set; } = string.Empty;
}

public record SurfaceMultiplier
{
    public required string SurfaceType { get; set; } = string.Empty;

    public required double VolumeMult { get; set; }
}

public record HeadphonesSettings
{
    public required double FadeDuration { get; set; }

    public required string FadeIn { get; set; } = string.Empty;

    public required string FadeOut { get; set; } = string.Empty;
}

public record MasterMixerSettings
{
    public required List<ExposedParameterValue> ExposedParameters { get; set; } = [];
}

public record ExposedParameterValue
{
    public required string ExposedParameter { get; set; } = string.Empty;

    public required double Value { get; set; }
}

public record MetaXRAudioPluginSettings
{
    [JsonPropertyName("audioGroupAcousticSettings")]
    public required List<AudioGroupAcousticSettingsItem> AudioGroupAcousticSettings { get; set; } = [];

    public required bool EnabledPluginErrorChecker { get; set; }

    public required bool HardResetEnabled { get; set; }

    public required double OutputVolumeCheckCooldown { get; set; }

    public required double ResetWaitTime { get; set; }
}

public record AudioGroupAcousticSettingsItem
{
    [JsonPropertyName("acousticSettings")]
    public required AcousticSettings? AcousticSettings { get; set; }

    [JsonPropertyName("groupType")]
    public required string GroupType { get; set; } = string.Empty;
}

public record AcousticSettings
{
    [JsonPropertyName("enabledPrewarm")]
    public required bool EnabledPrewarm { get; set; }

    [JsonPropertyName("mono")]
    public required Mono Mono { get; set; }

    [JsonPropertyName("stereo")]
    public required Mono Stereo { get; set; }
}

public record Mono
{
    [JsonPropertyName("earlyReflectionsSendDb")]
    public required int EarlyReflectionsSendDb { get; set; }

    [JsonPropertyName("enabledReverb")]
    public required bool EnabledReverb { get; set; }

    [JsonPropertyName("reverbReach")]
    public required double ReverbReach { get; set; }

    [JsonPropertyName("reverbSendDb")]
    public required int ReverbSendDb { get; set; }
}

public record PlayerSettings
{
    public required int BaseMaxMovementRolloff { get; set; }

    public required int IndoorRolloffMult { get; set; }

    public required ItemInHandsSettings ItemInHandsSettings { get; set; }

    public required PointOfViewSoundValue MinStepSoundRolloffSpeedMult { get; set; }

    public required PointOfViewSoundValue MinStepSoundVolumeMult { get; set; }

    public required PointOfViewSoundValue MinStepSoundVolumeSpeedMult { get; set; }

    public required List<MovementRolloffMultiplier> MovementRolloffMultipliers { get; set; } = [];

    public required double OutdoorRolloffMult { get; set; }

    public required PointOfViewSoundValue SearchSoundVolume { get; set; }

    public required TinnitusEffectConfig TinnitusEffectConfig { get; set; }
}

public record MovementRolloffMultiplier
{
    public required string MovementState { get; set; }

    public required double RolloffMultiplier { get; set; }
}

public record OcclusionSettings
{
    [JsonPropertyName("audioGroupOcclusionSettings")]
    public required List<AudioGroupOcclusionSettingsItem> AudioGroupOcclusionSettings { get; set; } = [];

    [JsonPropertyName("locationOcclusionSettings")]
    public required LocationOcclusionSettings LocationOcclusionSettings { get; set; }
}

public record AudioGroupOcclusionSettingsItem
{
    [JsonPropertyName("groupType")]
    public required string GroupType { get; set; } = string.Empty;

    [JsonPropertyName("occlusionSettings")]
    public required OcclusionSettings2 OcclusionSettings { get; set; }
}

public record OcclusionSettings2
{
    [JsonPropertyName("indoorToOutdoorFactor")]
    public required double IndoorToOutdoorFactor { get; set; }

    [JsonPropertyName("maxQualityFactor")]
    public required double MaxQualityFactor { get; set; }

    [JsonPropertyName("obstructionEQPreset")]
    public required PropagationEQPreset ObstructionEQPreset { get; set; }

    [JsonPropertyName("obstructionInSameIndoorRoom")]
    public required bool ObstructionInSameIndoorRoom { get; set; }

    [JsonPropertyName("occlusionEnabled")]
    public required bool OcclusionEnabled { get; set; }

    [JsonPropertyName("occlusionIntensity")]
    public required int OcclusionIntensity { get; set; }

    [JsonPropertyName("outdoorToIndoorFactor")]
    public required double OutdoorToIndoorFactor { get; set; }

    [JsonPropertyName("propagationEQPreset")]
    public required PropagationEQPreset PropagationEQPreset { get; set; }

    [JsonPropertyName("rolloffScale")]
    public required double RolloffScale { get; set; }

    [JsonPropertyName("stairsHeightCurve")]
    public required StairsHeightCurve StairsHeightCurve { get; set; }

    [JsonPropertyName("useQualityCompression")]
    public required bool UseQualityCompression { get; set; }
}

public record StairsHeightCurve
{
    [JsonPropertyName("m_Curve")]
    public required List<MCurveItem> MCurve { get; set; } = [];

    [JsonPropertyName("m_PostInfinity")]
    public required int MPostInfinity { get; set; }

    [JsonPropertyName("m_PreInfinity")]
    public required int MPreInfinity { get; set; }

    [JsonPropertyName("m_RotationOrder")]
    public required int MRotationOrder { get; set; }

    [JsonPropertyName("serializedVersion")]
    public required string SerializedVersion { get; set; } = string.Empty;
}

public record MCurveItem
{
    [JsonPropertyName("inSlope")]
    public required double InSlope { get; set; }

    [JsonPropertyName("inWeight")]
    public required double InWeight { get; set; }

    [JsonPropertyName("outSlope")]
    public required double OutSlope { get; set; }

    [JsonPropertyName("outWeight")]
    public required double OutWeight { get; set; }

    [JsonPropertyName("serializedVersion")]
    public required string SerializedVersion { get; set; } = string.Empty;

    [JsonPropertyName("tangentMode")]
    public required int TangentMode { get; set; }

    [JsonPropertyName("time")]
    public required double Time { get; set; }

    [JsonPropertyName("value")]
    public required double Value { get; set; }

    [JsonPropertyName("weightedMode")]
    public required int WeightedMode { get; set; }
}

public record PropagationEQPreset
{
    [JsonPropertyName("distanceCoefficient")]
    public required double DistanceCoefficient { get; set; }

    [JsonPropertyName("environmentVolumeThresholds")]
    public required EnvironmentEqThresholds EnvironmentVolumeThresholds { get; set; }

    [JsonPropertyName("heightVolumeCurve")]
    public required StairsHeightCurve HeightVolumeCurve { get; set; }

    [JsonPropertyName("hpfSettings")]
    public required HpfSettings HpfSettings { get; set; }

    [JsonPropertyName("lpfSettings")]
    public required HpfSettings LpfSettings { get; set; }

    [JsonPropertyName("rotationCoefficient")]
    public required double RotationCoefficient { get; set; }

    [JsonPropertyName("volumeCurve")]
    public required StairsHeightCurve VolumeCurve { get; set; }
}

public record HpfSettings
{
    [JsonPropertyName("distanceCurve")]
    public required StairsHeightCurve DistanceCurve { get; set; }

    [JsonPropertyName("environmentEqThresholds")]
    public required EnvironmentEqThresholds EnvironmentEqThresholds { get; set; }

    [JsonPropertyName("frequencyCurve")]
    public required StairsHeightCurve FrequencyCurve { get; set; }

    [JsonPropertyName("heightCurve")]
    public required StairsHeightCurve HeightCurve { get; set; }

    [JsonPropertyName("positionEqThresholds")]
    public required PositionEqThresholds PositionEqThresholds { get; set; }

    [JsonPropertyName("resonanceCurve")]
    public required StairsHeightCurve ResonanceCurve { get; set; }
}

public record EnvironmentEqThresholds
{
    [JsonPropertyName("baseValue")]
    public required float BaseValue { get; set; }

    [JsonPropertyName("diffEnvironmentIsolated")]
    public required float DiffEnvironmentIsolated { get; set; }

    [JsonPropertyName("diffRoomsTypeIsolated")]
    public required float DiffRoomsTypeIsolated { get; set; }

    [JsonPropertyName("indoorIsolated")]
    public required float IndoorIsolated { get; set; }

    [JsonPropertyName("indoorToOutdoor")]
    public required float IndoorToOutdoor { get; set; }

    [JsonPropertyName("outdoorToIndoor")]
    public required float OutdoorToIndoor { get; set; }
}

public record PositionEqThresholds
{
    [JsonPropertyName("aboveFreq")]
    public required int AboveFreq { get; set; }

    [JsonPropertyName("behindFreq")]
    public required int BehindFreq { get; set; }

    [JsonPropertyName("belowFreq")]
    public required int BelowFreq { get; set; }

    [JsonPropertyName("levelFreq")]
    public required int LevelFreq { get; set; }
}

public record LocationOcclusionSettings
{
    [JsonPropertyName("commonSettings")]
    public required CommonSettings CommonSettings { get; set; }

    [JsonPropertyName("diffractionSettings")]
    public required DiffractionSettings DiffractionSettings { get; set; }

    [JsonPropertyName("propagationSettings")]
    public required PropagationSettings PropagationSettings { get; set; }

    [JsonPropertyName("reflectionSettings")]
    public required ReflectionSettings ReflectionSettings { get; set; }

    [JsonPropertyName("transmissionSettings")]
    public required TransmissionSettings TransmissionSettings { get; set; }
}

public record CommonSettings
{
    [JsonPropertyName("diffractionThreshold")]
    public required double DiffractionThreshold { get; set; }

    [JsonPropertyName("effectChangeThreshold")]
    public required double EffectChangeThreshold { get; set; }

    [JsonPropertyName("floorHeight")]
    public required double FloorHeight { get; set; }

    [JsonPropertyName("maxDistance")]
    public required int MaxDistance { get; set; }

    [JsonPropertyName("playerObstructionYOffset")]
    public required double PlayerObstructionYOffset { get; set; }

    [JsonPropertyName("positionChangeThreshold")]
    public required List<PositionChangeThresholdItem> PositionChangeThreshold { get; set; } = [];

    [JsonPropertyName("smoothingFactor")]
    public required int SmoothingFactor { get; set; }

    [JsonPropertyName("transmissionThreshold")]
    public required double TransmissionThreshold { get; set; }
}

public record PositionChangeThresholdItem
{
    [JsonPropertyName("audioQuality")]
    public required string AudioQuality { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public required double Value { get; set; }
}

public record DiffractionSettings
{
    [JsonPropertyName("edgeSearchRayCount")]
    public required List<PositionChangeThresholdItem> EdgeSearchRayCount { get; set; } = [];

    [JsonPropertyName("edgeSearchRayLength")]
    public required int EdgeSearchRayLength { get; set; }

    [JsonPropertyName("edgeValidationRayOffset")]
    public required double EdgeValidationRayOffset { get; set; }

    [JsonPropertyName("maxEdgeDist")]
    public required int MaxEdgeDist { get; set; }

    [JsonPropertyName("maxPathFactor")]
    public required int MaxPathFactor { get; set; }
}

public record PropagationSettings
{
    [JsonPropertyName("absoluteHeightWeight")]
    public required double AbsoluteHeightWeight { get; set; }

    [JsonPropertyName("diffractionExponent")]
    public required double DiffractionExponent { get; set; }

    [JsonPropertyName("distanceWeight")]
    public required double DistanceWeight { get; set; }

    [JsonPropertyName("heightExponent")]
    public required double HeightExponent { get; set; }

    [JsonPropertyName("maxSegmentLength")]
    public required int MaxSegmentLength { get; set; }

    [JsonPropertyName("minPortalCostPercent")]
    public required double MinPortalCostPercent { get; set; }

    [JsonPropertyName("relaxationIterations")]
    public required int RelaxationIterations { get; set; }

    [JsonPropertyName("routesCompressionFactorByQuality")]
    public required List<PositionChangeThresholdItem> RoutesCompressionFactorByQuality { get; set; } = [];

    [JsonPropertyName("segmentHeightWeightDown")]
    public required double SegmentHeightWeightDown { get; set; }

    [JsonPropertyName("segmentHeightWeightUp")]
    public required double SegmentHeightWeightUp { get; set; }

    [JsonPropertyName("typicalRoomHeight")]
    public required double TypicalRoomHeight { get; set; }
}

public record ReflectionSettings
{
    [JsonPropertyName("energyLossFactorPerReflection")]
    public required double EnergyLossFactorPerReflection { get; set; }

    [JsonPropertyName("initialRaysCount")]
    public required List<PositionChangeThresholdItem> InitialRaysCount { get; set; } = [];

    [JsonPropertyName("maxReflections")]
    public required int MaxReflections { get; set; }

    [JsonPropertyName("minEnergyAtMaxDistance")]
    public required double MinEnergyAtMaxDistance { get; set; }
}

public record TransmissionSettings
{
    [JsonPropertyName("absorptionPerUnit")]
    public required double AbsorptionPerUnit { get; set; }

    [JsonPropertyName("initialRaysCount")]
    public required List<PositionChangeThresholdItem> InitialRaysCount { get; set; } = [];

    [JsonPropertyName("listenerHeightSamplingOffset")]
    public required double ListenerHeightSamplingOffset { get; set; }

    [JsonPropertyName("minClearPathScore")]
    public required double MinClearPathScore { get; set; }

    [JsonPropertyName("minEnergyThreshold")]
    public required double MinEnergyThreshold { get; set; }

    [JsonPropertyName("obstacleMaxThickness")]
    public required int ObstacleMaxThickness { get; set; }

    [JsonPropertyName("obstacleMinThickness")]
    public required double ObstacleMinThickness { get; set; }

    [JsonPropertyName("raysWideningRadius")]
    public required double RaysWideningRadius { get; set; }

    [JsonPropertyName("sourceHeightSamplingOffset")]
    public required double SourceHeightSamplingOffset { get; set; }

    [JsonPropertyName("useRaycast")]
    public required bool UseRaycast { get; set; }
}

public record ItemInHandsSettings
{
    public required PointOfViewSoundValue ItemOperationsSpatialBlend { get; set; }

    public required PointOfViewSoundValue ItemOperationsVolumeMult { get; set; }

    public required PointOfViewSoundValue WeaponOperationsSpatialBlend { get; set; }

    public required PointOfViewSoundValue WeaponOperationsVolumeMult { get; set; }
}

public record TinnitusEffectConfig
{
    public required double EffectBaseDurationMultiplier { get; set; }

    public required int MaxEffectDuration { get; set; }
}

public record WeaponSettings
{
    public required PlaybackSettings PlaybackSettings { get; set; }

    public required PoolSettings PoolSettings { get; set; }
}

public record PlaybackSettings
{
    public required bool AdaptiveFadeEnabled { get; set; }

    public required double AdaptiveFadeLeadBeatsThreshold { get; set; }

    public required int AdaptiveFadeMaxMultiplier { get; set; }

    public required double AdaptiveFadeMaxSeconds { get; set; }

    public required int BurstFireFadeMultiplier { get; set; }

    public required int FireLoopBreakMultiplier { get; set; }

    public required int IndoorMaxDistance { get; set; }

    public required int IndoorSilencedMaxDistance { get; set; }

    public required double MaxClampPitch { get; set; }

    public required double MaxRandomPitch { get; set; }

    public required double MinClampPitch { get; set; }

    public required double MinRandomPitch { get; set; }

    public required double OcclusionThreshold { get; set; }
}

public record PoolSettings
{
    public required int DefaultPoolSize { get; set; }

    public required int MaxPoolSize { get; set; }

    public required int MinPoolSize { get; set; }
}

public record FramerateLimit
{
    public required int MaxFramerateGameLimit { get; set; }

    public required int MaxFramerateLobbyLimit { get; set; }

    public required int MinFramerateLimit { get; set; }
}

public record MemoryManagementSettings
{
    public required bool AggressiveGC { get; set; }

    public required int GigabytesRequiredToDisableGCDuringRaid { get; set; }

    public required bool HeapPreAllocationEnabled { get; set; }

    public required int HeapPreAllocationMB { get; set; }

    public required bool OverrideRamCleanerSettings { get; set; }

    public required bool RamCleanerEnabled { get; set; }
}

public record NetworkStateView
{
    public required int LossThreshold { get; set; }

    public required int RttThreshold { get; set; }
}

public record ReleaseProfiler
{
    public required bool Enabled { get; set; }

    public required int MaxRecords { get; set; }

    public required int RecordTriggerValue { get; set; }
}
