namespace backend.Models.Production;

public sealed class RuntimeEventEntity
{
    public Guid Id { get; set; }
    public int LineId { get; set; }
    public int LineNumber { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string PreviousValue { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}