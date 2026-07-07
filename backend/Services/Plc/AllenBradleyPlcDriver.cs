using backend.DTOs.Plc;
using backend.Interfaces.Plc;
using libplctag;
using libplctag.DataTypes.Simple;

namespace backend.Services.Plc;

public sealed class AllenBradleyPlcDriver : IPlcDriver
{
    public string DriverName => "AllenBradley";

    public async Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var probeTag = new TagTagInfo
            {
                Name = "@tags",
                Gateway = ipAddress,
                Path = "1,0",
                PlcType = PlcType.ControlLogix,
                Protocol = Protocol.ab_eip,
            };

            await Task.Run(() => probeTag.Initialize(), cancellationToken);
            await Task.Run(probeTag.Read, cancellationToken);

            var discoveredTags = probeTag.Value?.Length ?? 0;

            stopwatch.Stop();

            return new PlcConnectionResult
            {
                IsConnected = true,
                Driver = DriverName,
                IpAddress = ipAddress,
                ControllerName = "CompactLogix / ControlLogix controller",
                Firmware = null,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Message = discoveredTags > 0
                    ? $"Connected to Allen-Bradley PLC. Discovered {discoveredTags} tag{(discoveredTags == 1 ? string.Empty : "s")}."
                    : "Connected to Allen-Bradley PLC.",
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return Failure(ipAddress, stopwatch, "Timeout while connecting to PLC.");
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
            Driver = "AllenBradley",
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

        if (message.Contains("not found") || message.Contains("errornotfound"))
        {
            return "The PLC responded, but the discovery tag was not found or tag discovery is not supported on this controller.";
        }

        return $"Communication exception while connecting to PLC: {exception.Message}";
    }
}