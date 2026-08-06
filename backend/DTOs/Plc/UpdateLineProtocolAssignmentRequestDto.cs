namespace backend.DTOs.Plc;

public sealed class UpdateLineProtocolAssignmentRequestDto
{
    public string Manufacturer { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public int PresetVersion { get; set; }
    public int PollIntervalMs { get; set; } = 2000;
    public string RoutePath { get; set; } = "1,0";
    public string ProcessorType { get; set; } = "ControlLogix";
    public int? Rack { get; set; }
    public int? Slot { get; set; }
    public int ConnectionTimeoutMs { get; set; } = 3000;
    public int ReadTimeoutMs { get; set; } = 3000;
    public int RetryCount { get; set; } = 1;
    public int RetryDelayMs { get; set; } = 250;
}
