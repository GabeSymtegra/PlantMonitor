namespace backend.Models.Production;

public sealed class CompletedProductionRunEntity
{
    public Guid Id { get; set; }
    public int LineId { get; set; }
    public int LineNumber { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string PlcIp { get; set; } = string.Empty;
    public string FinalStatus { get; set; } = string.Empty;

    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public double RuntimeSeconds { get; set; }
    public double ProductionLength { get; set; }

    public double AutoTimeSeconds { get; set; }
    public double ManualTimeSeconds { get; set; }
    public double AutoPercentage { get; set; }
    public double ManualPercentage { get; set; }
    public bool IsDeleted { get; set; }
    public string? DeletedByUsername { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<CompletedProductionRunZoneStatEntity> ZoneStats { get; set; } = [];
}
