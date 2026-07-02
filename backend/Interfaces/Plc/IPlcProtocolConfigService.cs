using backend.DTOs.Plc;

namespace backend.Interfaces.Plc;

public interface IPlcProtocolConfigService
{
    IReadOnlyCollection<PlcPresetDto> GetPresets(string? manufacturer);
    LineProtocolAssignmentDto? GetAssignment(int lineId);
    bool TryUpsertAssignment(int lineId, UpdateLineProtocolAssignmentRequestDto request, out string? error);
    IReadOnlyCollection<EffectiveTagMappingDto>? GetEffectiveTags(int lineId, out string? error);
    TagValidationResponseDto ValidateTags(ValidateTagsRequestDto request);
    bool TryUpsertOverrides(int lineId, UpdateLineTagOverridesRequestDto request, out IReadOnlyCollection<EffectiveTagMappingDto>? effectiveTags, out string? error);
}
