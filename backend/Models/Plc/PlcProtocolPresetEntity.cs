namespace backend.Models.Plc;

public sealed class PlcProtocolPresetEntity
{
    public int Id { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public int PresetVersion { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<PlcProtocolPresetTagEntity> Tags { get; set; } = [];
}
