namespace backend.Models.Plc;

public sealed class PlcProtocolPresetTagEntity
{
    public int Id { get; set; }
    public int PresetId { get; set; }
    public PlcProtocolPresetEntity Preset { get; set; } = null!;
    public string TagKey { get; set; } = string.Empty;
    public string PlcAddress { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public decimal Scale { get; set; } = 1.0m;
    public bool IsRequired { get; set; } = true;
}
