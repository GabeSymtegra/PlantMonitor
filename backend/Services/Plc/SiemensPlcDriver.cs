using backend.DTOs.Plc;
using backend.Interfaces.Plc;
using S7Plc = S7.Net.Plc;
using S7.Net;

namespace backend.Services.Plc;

public sealed class SiemensPlcDriver : IPlcDriver
{
    public string DriverName => "Siemens";

    public async Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var plc = new S7Plc(CpuType.S71200, ipAddress, 0, 1);
            await Task.Run(plc.Open, cancellationToken);

            if (!plc.IsConnected)
            {
                stopwatch.Stop();
                return Failure(ipAddress, stopwatch, "PLC did not report an active connection.");
            }

            stopwatch.Stop();

            return new PlcConnectionResult
            {
                IsConnected = true,
                Driver = DriverName,
                IpAddress = ipAddress,
                ControllerName = "Siemens S7-1200 / S7-1500 CPU",
                Firmware = null,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Message = "Connected to Siemens PLC.",
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return Failure(ipAddress, stopwatch, "Timeout while connecting to PLC.");
        }
        catch (PlcException exception)
        {
            stopwatch.Stop();
            return Failure(ipAddress, stopwatch, MapMessage(exception));
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return Failure(ipAddress, stopwatch, MapMessage(exception));
        }
    }

    private static PlcConnectionResult Failure(string ipAddress, System.Diagnostics.Stopwatch stopwatch, string message)
    {
        return new PlcConnectionResult
        {
            IsConnected = false,
            Driver = "Siemens",
            IpAddress = ipAddress,
            ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
            Message = message,
        };
    }

    private static string MapMessage(Exception exception)
    {
        var message = exception.Message.ToLowerInvariant();

        if (message.Contains("timeout"))
        {
            return "Timeout while connecting to PLC.";
        }

        if (message.Contains("refused") || message.Contains("unreachable"))
        {
            return "PLC offline, network unreachable, or firewall blocked the connection.";
        }

        if (message.Contains("rack") || message.Contains("slot") || message.Contains("cpu"))
        {
            return "Invalid PLC connection settings for the selected driver.";
        }

        return $"Communication exception while connecting to PLC: {exception.Message}";
    }
}