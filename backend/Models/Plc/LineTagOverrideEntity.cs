namespace backend.Models.Plc;

public sealed class LineTagOverrideEntity
{
    public int Id { get; set; }
    public int LineId { get; set; }
    public LineProtocolAssignmentEntity Assignment { get; set; } = null!;
    public string TagKey { get; set; } = string.Empty;
    public string PlcAddress { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public decimal Scale { get; set; } = 1.0m;
    public bool IsRequired { get; set; } = true;
}
