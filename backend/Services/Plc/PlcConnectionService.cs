using System.Net;
using backend.DTOs.Plc;
using backend.Interfaces.Plc;

namespace backend.Services.Plc;

public sealed class PlcConnectionService : IPlcConnectionService
{
    private const string DriverErrorMessage = "Wrong driver selected. Choose AllenBradley or Siemens.";
    private const string InvalidIpMessage = "Invalid IP address. Enter a valid IPv4 address.";

    private readonly IReadOnlyDictionary<string, IPlcDriver> _drivers;

    public PlcConnectionService(params IPlcDriver[] drivers)
    {
        _drivers = drivers.ToDictionary(driver => driver.DriverName, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<PlcConnectionResult> TestConnectionAsync(PlcConnectionRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryResolveDriver(request.Driver, out var driver, out var driverError))
        {
            return CreateFailure(request, driverError ?? DriverErrorMessage);
        }

        if (!IsValidIpAddress(request.IpAddress))
        {
            return CreateFailure(request, InvalidIpMessage);
        }

        return await driver!.TestConnectionAsync(request.IpAddress.Trim(), request.Options, cancellationToken);
    }

    public bool TryResolveDriver(string? driverName, out IPlcDriver? driver, out string? errorMessage)
    {
        driver = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(driverName))
        {
            errorMessage = DriverErrorMessage;
            return false;
        }

        if (!_drivers.TryGetValue(driverName.Trim(), out driver))
        {
            errorMessage = DriverErrorMessage;
            return false;
        }

        return true;
    }

    public bool IsValidIpAddress(string? ipAddress)
    {
        return IPAddress.TryParse(ipAddress?.Trim(), out var parsed)
            && parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
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