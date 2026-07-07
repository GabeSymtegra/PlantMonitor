namespace backend.DTOs.Plc;

public sealed class PlcTagReadResultDto
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "unknown";
    public string? Value { get; set; }
    public DateTime LastReadUtc { get; set; }
    public bool? CanRead { get; set; }
    public bool? CanWrite { get; set; }
    public string? Error { get; set; }
}
