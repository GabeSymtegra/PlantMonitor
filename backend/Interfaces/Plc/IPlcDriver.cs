using backend.DTOs.Plc;

namespace backend.Interfaces.Plc;

public interface IPlcDriver
{
    string DriverName { get; }

    Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, CancellationToken cancellationToken = default);
}