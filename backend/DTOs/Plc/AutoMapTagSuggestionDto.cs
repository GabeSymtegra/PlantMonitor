namespace backend.DTOs.Plc;

public sealed class AutoMapTagSuggestionDto
{
    public string LogicalKey { get; set; } = string.Empty;
    public string PlcAddress { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
}