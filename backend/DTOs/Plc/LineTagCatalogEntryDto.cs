namespace backend.DTOs.Plc;

public sealed class LineTagCatalogEntryDto
{
    public string LogicalKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public string PlcAddress { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal Scale { get; set; } = 1.0m;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public int ReadFrequencyMs { get; set; } = 1000;
    public bool IsRequired { get; set; } = true;
}
