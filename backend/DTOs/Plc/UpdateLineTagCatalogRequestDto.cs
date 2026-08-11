namespace backend.DTOs.Plc;

public sealed class UpdateLineTagCatalogRequestDto
{
    public string Driver { get; set; } = string.Empty;
    public List<LineTagCatalogEntryDto> Tags { get; set; } = [];
}
