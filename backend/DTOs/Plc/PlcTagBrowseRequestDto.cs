namespace backend.DTOs.Plc;

public sealed class PlcTagBrowseRequestDto
{
    public string Driver { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? Search { get; set; }
}
