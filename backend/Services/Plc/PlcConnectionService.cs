using System.Net;
using backend.DTOs.Plc;
using backend.Interfaces.Plc;

namespace backend.Services.Plc;

public sealed class PlcConnectionService : IPlcConnectionService
{
    private readonly IReadOnlyDictionary<string, IPlcDriver> _drivers;

    public PlcConnectionService(params IPlcDriver[] drivers)
    {
        _drivers = drivers.ToDictionary(driver => driver.DriverName, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<PlcConnectionResult> TestConnectionAsync(PlcConnectionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Driver))
        {
            return CreateFailure(request, "Wrong driver selected. Choose AllenBradley or Siemens.");
        }

        if (!_drivers.TryGetValue(request.Driver.Trim(), out var driver))
        {
            return CreateFailure(request, "Wrong driver selected. Choose AllenBradley or Siemens.");
        }

        if (!IPAddress.TryParse(request.IpAddress?.Trim(), out _))
        {
            return CreateFailure(request, "Invalid IP address. Enter a valid IPv4 address.");
        }

        return await driver.TestConnectionAsync(request.IpAddress.Trim(), cancellationToken);
    }

    private static PlcConnectionResult CreateFailure(PlcConnectionRequest request, string message)
    {
        return new PlcConnectionResult
        {
            IsConnected = false,
            Driver = request.Driver?.Trim() ?? string.Empty,
            IpAddress = request.IpAddress?.Trim() ?? string.Empty,
            Message = message,
        };
    }
}