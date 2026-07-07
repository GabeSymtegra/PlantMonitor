namespace backend.DTOs.Plc;

public sealed class PlcConnectionRequest
{
    public string Driver { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}