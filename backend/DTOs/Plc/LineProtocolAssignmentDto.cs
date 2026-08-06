namespace backend.DTOs.Plc;

public sealed class LineProtocolAssignmentDto
{
    public int LineId { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public int PresetVersion { get; set; }
    public int PollIntervalMs { get; set; }
    public string RoutePath { get; set; } = "1,0";
    public string ProcessorType { get; set; } = "ControlLogix";
    public int? Rack { get; set; }
    public int? Slot { get; set; }
    public int ConnectionTimeoutMs { get; set; }
    public int ReadTimeoutMs { get; set; }
    public int RetryCount { get; set; }
    public int RetryDelayMs { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
