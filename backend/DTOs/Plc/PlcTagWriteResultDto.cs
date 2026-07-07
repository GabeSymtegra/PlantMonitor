namespace backend.DTOs.Plc;

public sealed class PlcTagWriteResultDto
{
    public string Name { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime AttemptedAtUtc { get; set; }
    public string? Error { get; set; }
}
