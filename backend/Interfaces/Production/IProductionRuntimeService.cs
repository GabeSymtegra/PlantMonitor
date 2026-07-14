using backend.DTOs.Runtime;
using backend.DTOs.Production;

namespace backend.Interfaces.Production;

public interface IProductionRuntimeService
{
    DashboardSnapshotDto GetDashboardSnapshot();
    LineDetailSnapshotDto? GetLineDetail(int lineId);
    Task<IReadOnlyCollection<CompletedProductionRunDto>> GetCompletedRunsAsync(int? lineId, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RuntimeEventDto>> GetRuntimeEventsAsync(int? lineId, int take, CancellationToken cancellationToken = default);
}
