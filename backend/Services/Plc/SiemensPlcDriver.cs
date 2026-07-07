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

    public Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsAsync(
        string ipAddress,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = FillerTags.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(tag => tag.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var tags = query
            .Select(tag => new PlcTagBrowseItemDto
            {
                Name = tag.Name,
                DataType = tag.DataType,
                IsFolder = tag.IsFolder,
                ParentPath = tag.ParentPath,
                CanRead = tag.IsFolder ? null : true,
                CanWrite = tag.IsFolder ? null : tag.CanWrite,
            })
            .OrderBy(tag => tag.IsFolder ? 0 : 1)
            .ThenBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyCollection<PlcTagBrowseItemDto>>(tags);
    }

    public Task<PlcTagReadResultDto> ReadTagAsync(
        string ipAddress,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tag = FillerTags.FirstOrDefault(x =>
            !x.IsFolder && x.Name.Equals(tagName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (tag is null)
        {
            return Task.FromResult(new PlcTagReadResultDto
            {
                Name = tagName,
                LastReadUtc = DateTime.UtcNow,
                Error = "Tag was not found in filler mode.",
            });
        }

        return Task.FromResult(new PlcTagReadResultDto
        {
            Name = tag.Name,
            DataType = tag.DataType,
            Value = tag.SampleValue,
            LastReadUtc = DateTime.UtcNow,
            CanRead = true,
            CanWrite = tag.CanWrite,
        });
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

        var tag = FillerTags.FirstOrDefault(x =>
            !x.IsFolder && x.Name.Equals(tagName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (tag is null)
        {
            return Task.FromResult(new PlcTagWriteResultDto
            {
                Name = tagName,
                Success = false,
                AttemptedAtUtc = DateTime.UtcNow,
                Error = "Tag was not found in filler mode.",
            });
        }

        if (!tag.CanWrite)
        {
            return Task.FromResult(new PlcTagWriteResultDto
            {
                Name = tag.Name,
                Success = false,
                AttemptedAtUtc = DateTime.UtcNow,
                Error = "Tag is read-only in filler mode.",
            });
        }

        return Task.FromResult(new PlcTagWriteResultDto
        {
            Name = tag.Name,
            Success = true,
            AttemptedAtUtc = DateTime.UtcNow,
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