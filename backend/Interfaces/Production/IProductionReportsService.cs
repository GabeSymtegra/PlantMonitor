using backend.DTOs.Common;
using backend.DTOs.Production;

namespace backend.Interfaces.Production;

public interface IProductionReportsService
{
    Task<PagedResultDto<CompletedProductionRunDto>> GetCompletedRunsAsync(
        int? lineId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
    Task<CompletedProductionRunDto?> GetCompletedRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCompletedRunAsync(
        Guid runId,
        string deletedByUsername,
        string deletedByRole,
        CancellationToken cancellationToken = default);
    Task<PagedResultDto<RuntimeEventDto>> GetRuntimeEventsAsync(
        int? lineId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}