namespace backend.DTOs.Plc;

public sealed class UpdateLineTagOverridesRequestDto
{
    public List<EffectiveTagMappingDto> Tags { get; set; } = [];
}
