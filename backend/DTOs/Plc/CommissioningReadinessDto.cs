namespace backend.DTOs.Plc;

public sealed class CommissioningReadinessDto
{
    public int LineId { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public int PresetVersion { get; set; }
    public bool IsReady { get; set; }
    public int RequiredTagCount { get; set; }
    public int MappedRequiredTagCount { get; set; }
    public IReadOnlyCollection<string> MissingRequiredTagKeys { get; set; } = [];
    public IReadOnlyCollection<string> Issues { get; set; } = [];
    public DateTime CheckedAtUtc { get; set; }
}