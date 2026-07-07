using backend.DTOs.Plc;

namespace backend.Interfaces.Plc;

public interface IPlcConnectionService
{
    Task<PlcConnectionResult> TestConnectionAsync(PlcConnectionRequest request, CancellationToken cancellationToken = default);

    bool TryResolveDriver(string? driverName, out IPlcDriver? driver, out string? errorMessage);

    bool IsValidIpAddress(string? ipAddress);
}