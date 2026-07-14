namespace backend.DTOs.Plc;

public sealed class AutoMapTagCatalogResultDto
{
    public string Driver { get; set; } = string.Empty;
    public int ScannedTagCount { get; set; }
    public IReadOnlyCollection<LineTagCatalogEntryDto> SuggestedMappings { get; set; } = [];
    public IReadOnlyCollection<string> MissingLogicalKeys { get; set; } = [];
}
