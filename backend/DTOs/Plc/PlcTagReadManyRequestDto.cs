namespace backend.DTOs.Plc;

public sealed class PlcTagReadManyRequestDto
{
    public string Driver { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public IReadOnlyCollection<string> TagNames { get; set; } = [];
}
