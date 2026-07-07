namespace backend.DTOs.Plc;

public sealed class PlcTagWriteRequestDto
{
    public string Driver { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
