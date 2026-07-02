namespace backend.Models.Plc;

public sealed class LineProtocolAssignmentEntity
{
    public int LineId { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public int PresetVersion { get; set; }
    public int PollIntervalMs { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<LineTagOverrideEntity> TagOverrides { get; set; } = [];
}
