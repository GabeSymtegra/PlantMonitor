using backend.DTOs.Runtime;
using backend.DTOs.Production;
using backend.DTOs.Common;

namespace backend.Interfaces.Production;

public interface IProductionRuntimeService
{
    DashboardSnapshotDto GetDashboardSnapshot();
    LineDetailSnapshotDto? GetLineDetail(int lineId);
    bool IsKnownLine(int lineId);
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
