using backend.DTOs.Plc;

namespace backend.Interfaces.Plc;

public interface IPlcConnectionService
{
    Task<PlcConnectionResult> TestConnectionAsync(PlcConnectionRequest request, CancellationToken cancellationToken = default);
}