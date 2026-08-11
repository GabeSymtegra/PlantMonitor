using backend.DTOs.System;

namespace backend.Interfaces;

public interface IOtaReleaseService
{
    Task<OtaReleaseCheckDto> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
