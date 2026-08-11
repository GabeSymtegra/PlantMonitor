using backend.DTOs.System;

namespace backend.Interfaces;

public interface IOtaPackageStagingService
{
    Task<OtaStageOperationStatusDto> StagePackageAsync(OtaStagePackageRequestDto request, CancellationToken cancellationToken = default);
    OtaStageOperationStatusDto? GetStatus(string operationId);
}
