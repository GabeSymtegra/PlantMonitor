using backend.DTOs.System;

namespace backend.Interfaces;

public interface IOtaApplyOrchestrationService
{
    Task<OtaApplyOperationStatusDto> ApplyAsync(OtaApplyRequestDto request, CancellationToken cancellationToken = default);
    OtaApplyOperationStatusDto? GetStatus(string operationId);
}
