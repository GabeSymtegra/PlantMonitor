namespace backend.Models.Production;

public sealed class RecipeToleranceEntity
{
    public int Id { get; set; }
    public string RecipeId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string MeasurementType { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public decimal ToleranceMinus { get; set; }
    public decimal TolerancePlus { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string Source { get; set; } = "seed";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
