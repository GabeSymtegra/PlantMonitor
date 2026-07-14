namespace backend.DTOs.Production;

public sealed class RecipeToleranceDto
{
    public string RecipeId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string MeasurementType { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public decimal ToleranceMinus { get; set; }
    public decimal TolerancePlus { get; set; }
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
}
