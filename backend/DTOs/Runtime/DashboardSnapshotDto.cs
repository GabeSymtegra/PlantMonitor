namespace backend.DTOs.Runtime;

public sealed class DashboardSnapshotDto
{
    public IReadOnlyCollection<DashboardLineDto> Lines { get; set; } = [];
    public DateTime LastUpdatedUtc { get; set; }
}

public sealed class DashboardLineDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public string Status { get; set; } = "Offline";
    public string ControlMode { get; set; } = "Auto";
    public double TotalLength { get; set; }
    public long RuntimeSeconds { get; set; }
    public string PlcIp { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public double PercentAutoMode { get; set; }
    public double PercentManualMode { get; set; }
    public double AutoModeVariance { get; set; }
    public double ManualModeVariance { get; set; }
    public double TotalVariance { get; set; }
}
