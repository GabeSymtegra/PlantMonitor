namespace backend.Models.Plc;

public sealed class LineProtocolAssignmentEntity
{
    public int LineId { get; set; }
    public int LineNumber { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string PlcIp { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public int PresetVersion { get; set; }
    public int PollIntervalMs { get; set; }
    public string RoutePath { get; set; } = "1,0";
    public string ProcessorType { get; set; } = "ControlLogix";
    public int ConnectionTimeoutMs { get; set; } = 3000;
    public int ReadTimeoutMs { get; set; } = 3000;
    public int RetryCount { get; set; } = 1;
    public int RetryDelayMs { get; set; } = 250;
    public bool IsActive { get; set; }
    public string LineLifecycleState { get; set; } = Plc.LineLifecycleState.Draft;
    public DateTime UpdatedAtUtc { get; set; }
    public List<LineTagOverrideEntity> TagOverrides { get; set; } = [];
}
