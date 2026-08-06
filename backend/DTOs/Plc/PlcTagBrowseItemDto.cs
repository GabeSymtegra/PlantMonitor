namespace backend.DTOs.Plc;

public sealed class PlcTagBrowseItemDto
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string DataType { get; set; } = "unknown";
    public bool IsFolder { get; set; }
    public string? ParentPath { get; set; }
    public string? Description { get; set; }
    public bool? CanRead { get; set; }
    public bool? CanWrite { get; set; }
}
