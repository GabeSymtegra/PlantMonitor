namespace backend.DTOs.Plc;

public sealed class AutoMapTagCatalogRequestDto
{
    public string Driver { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public PlcConnectionOptionsDto? Options { get; set; }
    public string? Search { get; set; }
}
