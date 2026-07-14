namespace backend.DTOs.Production;

public sealed class RuntimeEventDto
{
    public Guid Id { get; set; }
    public int LineId { get; set; }
    public int LineNumber { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string PreviousValue { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}