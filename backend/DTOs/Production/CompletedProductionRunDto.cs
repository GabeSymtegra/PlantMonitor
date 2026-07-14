namespace backend.DTOs.Production;

public sealed class CompletedProductionRunDto
{
    public Guid Id { get; set; }
    public int LineId { get; set; }
    public int LineNumber { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string FinalStatus { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public double RuntimeSeconds { get; set; }
    public double ProductionLength { get; set; }
    public double AutoTimeSeconds { get; set; }
    public double ManualTimeSeconds { get; set; }
    public double AutoPercentage { get; set; }
    public double ManualPercentage { get; set; }
    public IReadOnlyCollection<CompletedProductionRunZoneStatDto> ZoneStats { get; set; } = [];
}

public sealed class CompletedProductionRunZoneStatDto
{
    public string Zone { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public long MeasurementCount { get; set; }
    public long SkippedCount { get; set; }
    public double AverageAbsoluteDeviation { get; set; }
    public double MaxPositiveDeviation { get; set; }
    public double MaxNegativeDeviation { get; set; }
    public double CurrentDeviation { get; set; }
}
