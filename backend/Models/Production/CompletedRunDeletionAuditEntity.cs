namespace backend.Models.Production;

public sealed class CompletedRunDeletionAuditEntity
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public int LineId { get; set; }
    public int LineNumber { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string DeletedByUsername { get; set; } = string.Empty;
    public string DeletedByRole { get; set; } = string.Empty;
    public DateTime DeletedAtUtc { get; set; }
}
