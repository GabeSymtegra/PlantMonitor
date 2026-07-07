namespace backend.DTOs.Plc;

public sealed class PlcConnectionResult
{
    public bool IsConnected { get; set; }
    public string Driver { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? ControllerName { get; set; }
    public string? Firmware { get; set; }
    public int? ResponseTimeMs { get; set; }
    public string Message { get; set; } = string.Empty;
}