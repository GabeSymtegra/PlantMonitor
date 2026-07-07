using backend.DTOs.Plc;
using backend.Interfaces.Plc;
using libplctag;
using libplctag.DataTypes.Simple;

namespace backend.Services.Plc;

public sealed class AllenBradleyPlcDriver : IPlcDriver
{
    private static readonly IReadOnlyList<FillerTagDefinition> FillerTags =
    [
        new("Machine", "folder", true, null, null, true),
        new("Machine.Status", "int", false, "Machine", "2", false),
        new("Machine.LineSpeed", "real", false, "Machine", "125.4", false),
        new("Machine.CurrentDiameter", "real", false, "Machine", "2.125", false),
        new("Machine.TargetLineSpeed", "real", true, "Machine", "130.0", false),
        new("Alarm", "folder", true, null, null, true),
        new("Alarm.AlarmCode", "dint", false, "Alarm", "0", false),
    ];

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