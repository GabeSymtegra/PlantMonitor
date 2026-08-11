namespace backend.Services.Production;

public enum MeasurementZone
{
    BareOd,
    HotOd,
    ColdOd,
}

public enum ControlMode
{
    Auto,
    Manual,
}

public sealed record ProductionRunMetadata(
    int LineId,
    string ProductId,
    string RecipeId,
    string MachineId,
    string OperatorName,
    DateTime StartTimeUtc);

public sealed record ZoneSample(double Setpoint, double Actual);

public sealed record ProductionTelemetrySample(
    DateTime TimestampUtc,
    ControlMode Mode,
    double ProductionLength,
    IReadOnlyDictionary<MeasurementZone, ZoneSample> Zones);

public sealed class ZoneStatisticsSnapshot
{
    public long MeasurementCount { get; init; }
    public long SkippedCount { get; init; }
    public double RunningAbsoluteDeviationSum { get; init; }
    public double AverageAbsoluteDeviation { get; init; }
    public double MaxPositiveDeviation { get; init; }
    public double MaxNegativeDeviation { get; init; }
    public double CurrentDeviation { get; init; }
}

public sealed class ZoneLiveSnapshot
{
    public double CurrentSetpoint { get; init; }
    public double CurrentActual { get; init; }
    public double CurrentPercentDeviation { get; init; }
}

public sealed class ModeQualitySnapshot
{
    public required IReadOnlyDictionary<MeasurementZone, ZoneStatisticsSnapshot> Zones { get; init; }
}

public sealed class ProductionStatisticsSnapshot
{
    public required ProductionRunMetadata Metadata { get; init; }
    public required DateTime LastUpdateUtc { get; init; }
    public required ControlMode CurrentMode { get; init; }
    public required double CurrentProductionLength { get; init; }

    public required double AutoTimeSeconds { get; init; }
    public required double ManualTimeSeconds { get; init; }
    public required double TotalControlTimeSeconds { get; init; }
    public required double AutoPercentage { get; init; }
    public required double ManualPercentage { get; init; }

    public required IReadOnlyDictionary<MeasurementZone, ZoneLiveSnapshot> LiveZones { get; init; }
    public required IReadOnlyDictionary<MeasurementZone, ZoneStatisticsSnapshot> OverallZones { get; init; }
    public required ModeQualitySnapshot AutoQuality { get; init; }
    public required ModeQualitySnapshot ManualQuality { get; init; }
}

public sealed class CompletedProductionRecord
{
    public required ProductionStatisticsSnapshot Snapshot { get; init; }
    public required DateTime EndTimeUtc { get; init; }
}
