using backend.DTOs.Plc;
using backend.Interfaces.Plc;
using S7Plc = S7.Net.Plc;
using S7.Net;

namespace backend.Services.Plc;

public sealed class SiemensPlcDriver : IPlcDriver
{
    private static readonly IReadOnlyList<FillerTagDefinition> FillerTags =
    [
        new("Machine", "folder", true, null, null, true),
        new("Machine.Status", "int", false, "Machine", "2", false),
        new("Machine.LineSpeed", "real", false, "Machine", "118.9", false),
        new("Machine.CurrentDiameter", "real", false, "Machine", "2.060", false),
        new("Machine.TargetLineSpeed", "real", true, "Machine", "120.0", false),
        new("Alarm", "folder", true, null, null, true),
        new("Alarm.AlarmCode", "dint", false, "Alarm", "0", false),
    ];

    public string DriverName => "Siemens";

    public Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, PlcConnectionOptionsDto? options, CancellationToken cancellationToken = default)
    {
        return TestConnectionAsync(ipAddress, cancellationToken);
    }

    public async Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return new PlcConnectionResult
        {
            IsConnected = false,
            Driver = DriverName,
            IpAddress = ipAddress,
            Message = "Siemens monitoring is not supported in this build. Commissioning is blocked until real read support is completed.",
        };

        #pragma warning disable CS0162
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
        #pragma warning restore CS0162
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

    public Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        return BrowseTagsAsync(ipAddress, search, cancellationToken);
    }

    public Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsAsync(
        string ipAddress,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyCollection<PlcTagBrowseItemDto>>([]);
    }

    public Task<PlcTagReadResultDto> ReadTagAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        return ReadTagAsync(ipAddress, tagName, cancellationToken);
    }

    public Task<PlcTagReadResultDto> ReadTagAsync(
        string ipAddress,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PlcTagReadResultDto
        {
            Name = tagName,
            LastReadUtc = DateTime.UtcNow,
            CanRead = false,
            CanWrite = false,
            Error = "Siemens monitoring is not supported in this build.",
        });
    }

    public async Task<IReadOnlyCollection<PlcTagReadResultDto>> ReadTagsAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default)
    {
        return await ReadTagsAsync(ipAddress, tagNames, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PlcTagReadResultDto>> ReadTagsAsync(
        string ipAddress,
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var results = new List<PlcTagReadResultDto>(tagNames.Count);

        foreach (var tagName in tagNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            results.Add(await ReadTagAsync(ipAddress, tagName, cancellationToken));
        }

        return results;
    }

    public Task<PlcTagWriteResultDto> WriteTagAsync(
        string ipAddress,
        string tagName,
        string value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PlcTagWriteResultDto
        {
            Name = tagName,
            Success = false,
            AttemptedAtUtc = DateTime.UtcNow,
            Error = "Write operations are disabled. PlantMonitor is monitor-only.",
        });
    }

    private sealed record FillerTagDefinition(
        string Name,
        string DataType,
        bool CanWrite,
        string? ParentPath,
        string? SampleValue,
        bool IsFolder);
}