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
    public bool IsActive { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<LineTagOverrideEntity> TagOverrides { get; set; } = [];
}
