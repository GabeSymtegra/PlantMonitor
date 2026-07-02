namespace backend.DTOs.Plc;

public sealed class TagValidationIssueDto
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
