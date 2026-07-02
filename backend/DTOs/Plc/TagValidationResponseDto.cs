namespace backend.DTOs.Plc;

public sealed class TagValidationResponseDto
{
    public bool IsValid { get; set; }
    public List<TagValidationIssueDto> Issues { get; set; } = [];
}
