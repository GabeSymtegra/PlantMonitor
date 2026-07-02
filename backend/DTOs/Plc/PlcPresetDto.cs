namespace backend.DTOs.Plc;

public sealed class PlcPresetDto
{
    public string Manufacturer { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public int PresetVersion { get; set; }
    public string Description { get; set; } = string.Empty;
    public IReadOnlyCollection<EffectiveTagMappingDto> Tags { get; set; } = [];
}
