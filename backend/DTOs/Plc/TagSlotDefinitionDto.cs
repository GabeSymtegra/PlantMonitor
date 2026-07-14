namespace backend.DTOs.Plc;

public sealed class TagSlotDefinitionDto
{
    public string LogicalKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}
