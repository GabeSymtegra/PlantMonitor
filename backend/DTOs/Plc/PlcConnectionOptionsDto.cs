namespace backend.DTOs.Plc;

public sealed class PlcConnectionOptionsDto
{
    public string RoutePath { get; set; } = "1,0";
    public string ProcessorType { get; set; } = "ControlLogix";
    public int ConnectionTimeoutMs { get; set; } = 3000;
    public int ReadTimeoutMs { get; set; } = 3000;
    public int RetryCount { get; set; } = 1;
    public int RetryDelayMs { get; set; } = 250;
}
