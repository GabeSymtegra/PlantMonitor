namespace backend.DTOs.Plc;

public sealed class ValidateTagsRequestDto
{
    public string Manufacturer { get; set; } = string.Empty;
    public int PollIntervalMs { get; set; } = 2000;
    public List<EffectiveTagMappingDto> Tags { get; set; } = [];
}
