namespace backend.DTOs.Plc;

public sealed class EffectiveTagMappingDto
{
    public string TagKey { get; set; } = string.Empty;
    public string PlcAddress { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public decimal Scale { get; set; } = 1.0m;
    public bool IsRequired { get; set; } = true;
}
