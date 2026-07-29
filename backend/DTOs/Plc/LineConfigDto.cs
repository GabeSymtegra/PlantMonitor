namespace backend.DTOs.Plc;

public sealed class LineConfigDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string PlcIp { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public int PollIntervalMs { get; set; }
    public bool IsActive { get; set; }
    public string LineLifecycleState { get; set; } = Models.Plc.LineLifecycleState.Draft;
    public DateTime UpdatedAtUtc { get; set; }
}