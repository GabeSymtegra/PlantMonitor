namespace backend.DTOs.Runtime;

public sealed class LineDetailSnapshotDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string PlcIp { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Status { get; set; } = "Offline";
    public string ControlMode { get; set; } = "Auto";
    public DateTime LastUpdatedUtc { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public double CurrentProductionLength { get; set; }
    public long RuntimeSeconds { get; set; }

    public double AutoTimeSeconds { get; set; }
    public double ManualTimeSeconds { get; set; }
    public double AutoPercentage { get; set; }
    public double ManualPercentage { get; set; }

    public IReadOnlyCollection<SensorSectionDto> Sensors { get; set; } = [];
}

public sealed class SensorSectionDto
{
    public string Zone { get; set; } = string.Empty;
    public double CurrentSetpoint { get; set; }
    public double CurrentActual { get; set; }
    public double CurrentPercentDeviation { get; set; }

    public long OverallMeasurementCount { get; set; }
    public double OverallAverageAbsoluteDeviation { get; set; }
    public double OverallMaxPositiveDeviation { get; set; }
    public double OverallMaxNegativeDeviation { get; set; }

    public long AutoMeasurementCount { get; set; }
    public double AutoAverageAbsoluteDeviation { get; set; }
    public double AutoMaxPositiveDeviation { get; set; }
    public double AutoMaxNegativeDeviation { get; set; }

    public long ManualMeasurementCount { get; set; }
    public double ManualAverageAbsoluteDeviation { get; set; }
    public double ManualMaxPositiveDeviation { get; set; }
    public double ManualMaxNegativeDeviation { get; set; }
}
