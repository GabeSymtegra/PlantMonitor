namespace backend.Models.Production;

public sealed class CompletedProductionRunZoneStatEntity
{
    public int Id { get; set; }
    public Guid RunId { get; set; }
    public CompletedProductionRunEntity? Run { get; set; }

    public string Zone { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;

    public long MeasurementCount { get; set; }
    public long SkippedCount { get; set; }
    public double RunningAbsoluteDeviationSum { get; set; }
    public double AverageAbsoluteDeviation { get; set; }
    public double MaxPositiveDeviation { get; set; }
    public double MaxNegativeDeviation { get; set; }
    public double CurrentDeviation { get; set; }
}
