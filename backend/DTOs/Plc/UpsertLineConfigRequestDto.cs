namespace backend.DTOs.Plc;

public sealed class UpsertLineConfigRequestDto
{
    public int LineNumber { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string PlcIp { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public int PollIntervalMs { get; set; } = 2000;
    public bool IsActive { get; set; } = true;
}