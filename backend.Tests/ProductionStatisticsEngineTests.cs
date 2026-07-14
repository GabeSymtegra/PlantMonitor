using backend.Services.Production;
using Xunit;

namespace backend.Tests;

public sealed class ProductionStatisticsEngineTests
{
    [Fact]
    public void ApplySample_ComputesIndependentZoneStats_WithoutCrossOver()
    {
        var engine = CreateStartedEngine();

        engine.ApplySample(new ProductionTelemetrySample(
            TimestampUtc: new DateTime(2026, 7, 14, 10, 0, 1, DateTimeKind.Utc),
            Mode: ControlMode.Auto,
            ProductionLength: 125,
            Zones: new Dictionary<MeasurementZone, ZoneSample>
            {
                [MeasurementZone.BareOd] = new(100, 101),
                [MeasurementZone.HotOd] = new(200, 198),
                [MeasurementZone.ColdOd] = new(300, 300),
            }));

        var snapshot = engine.GetSnapshot();

        Assert.Equal(1, snapshot.OverallZones[MeasurementZone.BareOd].MeasurementCount);
        Assert.Equal(1d, snapshot.OverallZones[MeasurementZone.BareOd].CurrentDeviation, 6);

        Assert.Equal(1, snapshot.OverallZones[MeasurementZone.HotOd].MeasurementCount);
        Assert.Equal(-1d, snapshot.OverallZones[MeasurementZone.HotOd].CurrentDeviation, 6);

        Assert.Equal(1, snapshot.OverallZones[MeasurementZone.ColdOd].MeasurementCount);
        Assert.Equal(0d, snapshot.OverallZones[MeasurementZone.ColdOd].CurrentDeviation, 6);
    }

    [Fact]
    public void ApplySamples_TracksAutoAndManualModeSeparately()
    {
        var engine = CreateStartedEngine();

        engine.ApplySample(new ProductionTelemetrySample(
            TimestampUtc: new DateTime(2026, 7, 14, 10, 0, 1, DateTimeKind.Utc),
            Mode: ControlMode.Auto,
            ProductionLength: 10,
            Zones: new Dictionary<MeasurementZone, ZoneSample>
            {
                [MeasurementZone.BareOd] = new(100, 100.2),
            }));

        engine.ApplySample(new ProductionTelemetrySample(
            TimestampUtc: new DateTime(2026, 7, 14, 10, 0, 6, DateTimeKind.Utc),
            Mode: ControlMode.Manual,
            ProductionLength: 40,
            Zones: new Dictionary<MeasurementZone, ZoneSample>
            {
                [MeasurementZone.BareOd] = new(100, 99.0),
            }));

        var snapshot = engine.GetSnapshot();

        Assert.Equal(0.2d, snapshot.AutoQuality.Zones[MeasurementZone.BareOd].AverageAbsoluteDeviation, 6);
        Assert.Equal(1.0d, snapshot.ManualQuality.Zones[MeasurementZone.BareOd].AverageAbsoluteDeviation, 6);
        Assert.Equal(2, snapshot.OverallZones[MeasurementZone.BareOd].MeasurementCount);
        Assert.Equal(0.6d, snapshot.OverallZones[MeasurementZone.BareOd].AverageAbsoluteDeviation, 6);
    }

    [Fact]
    public void ApplySamples_TracksModeTimeAndPercentages()
    {
        var engine = CreateStartedEngine();

        engine.ApplySample(new ProductionTelemetrySample(
            TimestampUtc: new DateTime(2026, 7, 14, 10, 0, 2, DateTimeKind.Utc),
            Mode: ControlMode.Auto,
            ProductionLength: 5,
            Zones: new Dictionary<MeasurementZone, ZoneSample>()));

        engine.ApplySample(new ProductionTelemetrySample(
            TimestampUtc: new DateTime(2026, 7, 14, 10, 0, 8, DateTimeKind.Utc),
            Mode: ControlMode.Manual,
            ProductionLength: 15,
            Zones: new Dictionary<MeasurementZone, ZoneSample>()));

        var snapshot = engine.GetSnapshot();

        Assert.Equal(2d, snapshot.AutoTimeSeconds, 6);
        Assert.Equal(6d, snapshot.ManualTimeSeconds, 6);
        Assert.Equal(8d, snapshot.TotalControlTimeSeconds, 6);
        Assert.Equal(25d, snapshot.AutoPercentage, 6);
        Assert.Equal(75d, snapshot.ManualPercentage, 6);
    }

    [Fact]
    public void ApplySamples_TracksMaxPositiveAndNegativeDeviation()
    {
        var engine = CreateStartedEngine();

        engine.ApplySample(new ProductionTelemetrySample(
            TimestampUtc: new DateTime(2026, 7, 14, 10, 0, 1, DateTimeKind.Utc),
            Mode: ControlMode.Auto,
            ProductionLength: 1,
            Zones: new Dictionary<MeasurementZone, ZoneSample>
            {
                [MeasurementZone.HotOd] = new(100, 102),
            }));

        engine.ApplySample(new ProductionTelemetrySample(
            TimestampUtc: new DateTime(2026, 7, 14, 10, 0, 2, DateTimeKind.Utc),
            Mode: ControlMode.Auto,
            ProductionLength: 2,
            Zones: new Dictionary<MeasurementZone, ZoneSample>
            {
                [MeasurementZone.HotOd] = new(100, 95),
            }));

        var stats = engine.GetSnapshot().OverallZones[MeasurementZone.HotOd];

        Assert.Equal(2d, stats.MaxPositiveDeviation, 6);
        Assert.Equal(-5d, stats.MaxNegativeDeviation, 6);
        Assert.Equal(-5d, stats.CurrentDeviation, 6);
    }

    [Fact]
    public void ApplySample_WithZeroSetpoint_IncrementsSkippedWithoutMeasurement()
    {
        var engine = CreateStartedEngine();

        engine.ApplySample(new ProductionTelemetrySample(
            TimestampUtc: new DateTime(2026, 7, 14, 10, 0, 1, DateTimeKind.Utc),
            Mode: ControlMode.Auto,
            ProductionLength: 5,
            Zones: new Dictionary<MeasurementZone, ZoneSample>
            {
                [MeasurementZone.ColdOd] = new(0, 10),
            }));

        var overall = engine.GetSnapshot().OverallZones[MeasurementZone.ColdOd];
        Assert.Equal(0, overall.MeasurementCount);
        Assert.Equal(1, overall.SkippedCount);
        Assert.Equal(0d, overall.AverageAbsoluteDeviation, 6);
    }

    [Fact]
    public void CompleteRun_ReturnsSnapshotAndMarksEngineInactive()
    {
        var engine = CreateStartedEngine();

        engine.ApplySample(new ProductionTelemetrySample(
            TimestampUtc: new DateTime(2026, 7, 14, 10, 0, 1, DateTimeKind.Utc),
            Mode: ControlMode.Auto,
            ProductionLength: 1,
            Zones: new Dictionary<MeasurementZone, ZoneSample>
            {
                [MeasurementZone.BareOd] = new(100, 101),
            }));

        var completed = engine.CompleteRun(new DateTime(2026, 7, 14, 10, 5, 0, DateTimeKind.Utc));

        Assert.Equal(1, completed.Snapshot.OverallZones[MeasurementZone.BareOd].MeasurementCount);
        Assert.False(engine.IsActive);
    }

    private static ProductionStatisticsEngine CreateStartedEngine()
    {
        var engine = new ProductionStatisticsEngine();
        engine.StartRun(new ProductionRunMetadata(
            LineId: 1,
            ProductId: "1/0",
            RecipeId: "R-001",
            MachineId: "M-01",
            OperatorName: "operator-a",
            StartTimeUtc: new DateTime(2026, 7, 14, 10, 0, 0, DateTimeKind.Utc)));

        return engine;
    }
}
