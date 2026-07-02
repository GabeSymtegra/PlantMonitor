namespace backend.DTOs.Plc;

public sealed class UpdateLineProtocolAssignmentRequestDto
{
    public string Manufacturer { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public int PresetVersion { get; set; }
    public int PollIntervalMs { get; set; } = 2000;
}
