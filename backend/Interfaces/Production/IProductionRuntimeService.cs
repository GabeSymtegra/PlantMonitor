using backend.DTOs.Runtime;
using backend.DTOs.Production;

namespace backend.Interfaces.Production;

public interface IProductionRuntimeService
{
    DashboardSnapshotDto GetDashboardSnapshot();
    LineDetailSnapshotDto? GetLineDetail(int lineId);
    bool IsKnownLine(int lineId);
}
