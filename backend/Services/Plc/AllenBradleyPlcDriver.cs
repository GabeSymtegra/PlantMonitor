using backend.DTOs.Plc;
using backend.Interfaces.Plc;
using libplctag;
using libplctag.DataTypes.Simple;
using System.Text.Json;
using System.Collections.Concurrent;
using TagInfo = libplctag.DataTypes.TagInfo;

namespace backend.Services.Plc;

// Allen-Bradley support is built around live controller discovery and leaf-tag
// reads. When direct reads are unavailable, the driver returns diagnostics
// instead of inventing values.
public sealed class AllenBradleyPlcDriver : IPlcDriver
{
    private static readonly ConcurrentDictionary<string, (DateTime CachedAtUtc, TagInfo[] Tags)> TagCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan TagCacheTtl = TimeSpan.FromSeconds(30);

    // Common primitive type codes returned by libplctag metadata.
    private const ushort BoolTypeCode = 0x00C1;
    private const ushort SintTypeCode = 0x00C2;
    private const ushort IntTypeCode = 0x00C3;
    private const ushort DintTypeCode = 0x00C4;
    private const ushort LintTypeCode = 0x00C5;
    private const ushort RealTypeCode = 0x00CA;
    private const ushort LrealTypeCode = 0x00CB;

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

    // -------------------------------------------------------------------------
    // Connection and top-level driver operations
    // -------------------------------------------------------------------------

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
            var discoveredTags = 0;

            try
            {
                await Task.Run(probeTag.Read, cancellationToken);
                discoveredTags = probeTag.Value?.Length ?? 0;
            }
            catch (Exception discoveryException) when (IsDiscoveryUnsupported(discoveryException))
            {
                stopwatch.Stop();

                return new PlcConnectionResult
                {
                    IsConnected = true,
                    Driver = DriverName,
                    IpAddress = ipAddress,
                    ControllerName = "CompactLogix / ControlLogix controller",
                    Firmware = null,
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    Message = "Connected to Allen-Bradley PLC. Tag discovery is limited on this controller.",
                };
            }

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

    // Older or restricted controllers may respond to the probe but not support
    // the discovery tag used for browse-style operations.
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

    private static bool IsDiscoveryUnsupported(Exception exception)
    {
        var message = exception.Message.ToLowerInvariant();
        return message.Contains("not found")
            || message.Contains("errornotfound")
            || message.Contains("tag discovery")
            || message.Contains("discovery");
    }

    // Browse, read, and write follow the same driver contract used by the
    // admin PLC tooling and runtime service.
    public Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsAsync(
        string ipAddress,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        return BrowseLiveTagsAsync(ipAddress, search, cancellationToken);
    }

    public async Task<PlcTagReadResultDto> ReadTagAsync(
        string ipAddress,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var discoveredTags = await DiscoverTagsAsync(ipAddress, cancellationToken);
        var tag = discoveredTags.FirstOrDefault(x =>
            x.Name.Equals(tagName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (tag is null)
        {
            return new PlcTagReadResultDto
            {
                Name = tagName,
                LastReadUtc = DateTime.UtcNow,
                Error = "Tag was not found on the controller.",
            };
        }

        return await ReadLiveTagAsync(ipAddress, tag, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PlcTagReadResultDto>> ReadTagsAsync(
        string ipAddress,
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var discoveredTags = await DiscoverTagsAsync(ipAddress, cancellationToken);
        var discoveredMap = discoveredTags.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var results = new List<PlcTagReadResultDto>(tagNames.Count);

        foreach (var tagName in tagNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!discoveredMap.TryGetValue(tagName.Trim(), out var tag))
            {
                results.Add(new PlcTagReadResultDto
                {
                    Name = tagName,
                    LastReadUtc = DateTime.UtcNow,
                    Error = "Tag was not found on the controller.",
                });
                continue;
            }

            results.Add(await ReadLiveTagAsync(ipAddress, tag, cancellationToken));
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

    // -------------------------------------------------------------------------
    // Internal browse/read helpers
    // -------------------------------------------------------------------------

    private sealed record FillerTagDefinition(
        string Name,
        string DataType,
        bool CanWrite,
        string? ParentPath,
        string? SampleValue,
        bool IsFolder);

    private static async Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseLiveTagsAsync(
        string ipAddress,
        string? search,
        CancellationToken cancellationToken)
    {
        var discoveredTags = await DiscoverTagsAsync(ipAddress, cancellationToken);
        var searchTerm = search?.Trim();

        return discoveredTags
            .Where(tag => string.IsNullOrWhiteSpace(searchTerm)
                || tag.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .Select(tag => new PlcTagBrowseItemDto
            {
                Name = tag.Name,
                DataType = MapDataType(tag.Type),
                IsFolder = false,
                ParentPath = BuildParentPath(tag.Name),
                CanRead = true,
                CanWrite = false,
            })
            .OrderBy(tag => tag.ParentPath is null ? 0 : 1)
            .ThenBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<TagInfo[]> DiscoverTagsAsync(string ipAddress, CancellationToken cancellationToken)
    {
        if (TagCache.TryGetValue(ipAddress, out var cached)
            && DateTime.UtcNow - cached.CachedAtUtc < TagCacheTtl)
        {
            return cached.Tags;
        }

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

        var tags = probeTag.Value ?? [];
        TagCache[ipAddress] = (DateTime.UtcNow, tags);
        return tags;
    }

    private static async Task<PlcTagReadResultDto> ReadScalarTagAsync(
        string ipAddress,
        TagInfo tag,
        CancellationToken cancellationToken)
    {
        var dataType = MapDataType(tag.Type);

        try
        {
            var value = tag.Type switch
            {
                BoolTypeCode => await ReadScalarAsync(new TagBool(), ipAddress, tag.Name, cancellationToken),
                SintTypeCode => await ReadScalarAsync(new TagSint(), ipAddress, tag.Name, cancellationToken),
                IntTypeCode => await ReadScalarAsync(new TagInt(), ipAddress, tag.Name, cancellationToken),
                DintTypeCode => await ReadScalarAsync(new TagDint(), ipAddress, tag.Name, cancellationToken),
                LintTypeCode => await ReadScalarAsync(new TagLint(), ipAddress, tag.Name, cancellationToken),
                RealTypeCode => await ReadScalarAsync(new TagReal(), ipAddress, tag.Name, cancellationToken),
                LrealTypeCode => await ReadScalarAsync(new TagLreal(), ipAddress, tag.Name, cancellationToken),
                _ => null,
            };

            if (value is null)
            {
                return new PlcTagReadResultDto
                {
                    Name = tag.Name,
                    DataType = dataType,
                    LastReadUtc = DateTime.UtcNow,
                    CanRead = false,
                    CanWrite = false,
                    Error = $"Read is not currently supported for {dataType} tags in this view.",
                };
            }

            return new PlcTagReadResultDto
            {
                Name = tag.Name,
                DataType = dataType,
                Value = value,
                LastReadUtc = DateTime.UtcNow,
                CanRead = true,
                CanWrite = false,
            };
        }
        catch (Exception exception)
        {
            return new PlcTagReadResultDto
            {
                Name = tag.Name,
                DataType = dataType,
                LastReadUtc = DateTime.UtcNow,
                CanRead = false,
                CanWrite = false,
                Error = $"Unable to read tag value: {exception.Message}",
            };
        }
    }

    private static async Task<PlcTagReadResultDto> ReadLiveTagAsync(
        string ipAddress,
        TagInfo tag,
        CancellationToken cancellationToken)
    {
        if (IsSupportedScalarType(tag.Type) && IsScalarTag(tag))
        {
            return await ReadScalarTagAsync(ipAddress, tag, cancellationToken);
        }

        return await ReadRawTagAsync(ipAddress, tag, cancellationToken);
    }

    private static async Task<PlcTagReadResultDto> ReadRawTagAsync(
        string ipAddress,
        TagInfo tag,
        CancellationToken cancellationToken)
    {
        try
        {
            var rawTag = new Tag
            {
                Name = tag.Name,
                Gateway = ipAddress,
                Path = "1,0",
                PlcType = PlcType.ControlLogix,
                Protocol = Protocol.ab_eip,
            };

            var size = ResolveTagByteSize(tag);
            rawTag.SetSize(size);

            await Task.Run(rawTag.Initialize, cancellationToken);
            await Task.Run(rawTag.Read, cancellationToken);

            var buffer = rawTag.GetBuffer();
            var value = FormatRawTagValue(tag, buffer);

            return new PlcTagReadResultDto
            {
                Name = tag.Name,
                DataType = MapDataType(tag.Type),
                Value = value,
                LastReadUtc = DateTime.UtcNow,
                CanRead = true,
                CanWrite = false,
            };
        }
        catch (Exception exception)
        {
            var message = exception.Message;

            if (message.Contains("ErrorNotFound", StringComparison.OrdinalIgnoreCase))
            {
                return new PlcTagReadResultDto
                {
                    Name = tag.Name,
                    DataType = MapDataType(tag.Type),
                    Value = BuildStructuredReadDiagnosticPayload(tag),
                    LastReadUtc = DateTime.UtcNow,
                    CanRead = false,
                    CanWrite = false,
                    Error = "Direct read for this discovered symbol returned ErrorNotFound. This is usually a controller object or structured tag that requires member expansion.",
                };
            }

            return new PlcTagReadResultDto
            {
                Name = tag.Name,
                DataType = MapDataType(tag.Type),
                Value = BuildStructuredReadDiagnosticPayload(tag),
                LastReadUtc = DateTime.UtcNow,
                CanRead = false,
                CanWrite = false,
                Error = $"Unable to read tag value: {message}",
            };
        }
    }

    private static async Task<string?> ReadScalarAsync<TTag>(
        TTag tag,
        string ipAddress,
        string tagName,
        CancellationToken cancellationToken)
        where TTag : class, new()
    {
        var tagType = typeof(TTag);

        tagType.GetProperty("Name")?.SetValue(tag, tagName);
        tagType.GetProperty("Gateway")?.SetValue(tag, ipAddress);
        tagType.GetProperty("Path")?.SetValue(tag, "1,0");
        tagType.GetProperty("PlcType")?.SetValue(tag, PlcType.ControlLogix);
        tagType.GetProperty("Protocol")?.SetValue(tag, Protocol.ab_eip);

        await Task.Run(() => tagType.GetMethod("Initialize")?.Invoke(tag, null), cancellationToken);
        await Task.Run(() => tagType.GetMethod("Read")?.Invoke(tag, null), cancellationToken);

        var value = tagType.GetProperty("Value")?.GetValue(tag);
        return value?.ToString();
    }

    private static int ResolveTagByteSize(TagInfo tag)
    {
        if (tag.Length > 0)
        {
            return tag.Length;
        }

        var elementSize = GetPrimitiveByteSize(tag.Type);
        var elementCount = GetElementCount(tag.Dimensions);
        return Math.Max(1, elementSize * elementCount);
    }

    private static string FormatRawTagValue(TagInfo tag, byte[] buffer)
    {
        if (IsSupportedScalarType(tag.Type))
        {
            if (IsScalarTag(tag))
            {
                return FormatPrimitiveScalar(tag.Type, buffer);
            }

            var arrayValues = FormatPrimitiveArray(tag.Type, buffer, GetElementCount(tag.Dimensions));
            return JsonSerializer.Serialize(arrayValues);
        }

        var payload = new
        {
            kind = "raw",
            byteLength = buffer.Length,
            dimensions = tag.Dimensions,
            hex = BitConverter.ToString(buffer),
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string BuildStructuredReadDiagnosticPayload(TagInfo tag)
    {
        var payload = new
        {
            kind = "metadata",
            tag = tag.Name,
            dataType = MapDataType(tag.Type),
            byteLength = ResolveTagByteSize(tag),
            dimensions = tag.Dimensions,
            note = "The symbol was discovered successfully, but this top-level address is not directly readable as a scalar value.",
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string FormatPrimitiveScalar(ushort typeCode, byte[] buffer)
    {
        return typeCode switch
        {
            BoolTypeCode => (buffer[0] != 0).ToString(),
            SintTypeCode => ((sbyte)buffer[0]).ToString(),
            IntTypeCode => BitConverter.ToInt16(buffer, 0).ToString(),
            DintTypeCode => BitConverter.ToInt32(buffer, 0).ToString(),
            LintTypeCode => BitConverter.ToInt64(buffer, 0).ToString(),
            RealTypeCode => BitConverter.ToSingle(buffer, 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            LrealTypeCode => BitConverter.ToDouble(buffer, 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => BitConverter.ToString(buffer),
        };
    }

    private static object[] FormatPrimitiveArray(ushort typeCode, byte[] buffer, int elementCount)
    {
        var elementSize = GetPrimitiveByteSize(typeCode);
        var values = new object[elementCount];

        for (var index = 0; index < elementCount; index++)
        {
            var offset = index * elementSize;
            values[index] = typeCode switch
            {
                BoolTypeCode => buffer[offset] != 0,
                SintTypeCode => (sbyte)buffer[offset],
                IntTypeCode => BitConverter.ToInt16(buffer, offset),
                DintTypeCode => BitConverter.ToInt32(buffer, offset),
                LintTypeCode => BitConverter.ToInt64(buffer, offset),
                RealTypeCode => BitConverter.ToSingle(buffer, offset),
                LrealTypeCode => BitConverter.ToDouble(buffer, offset),
                _ => BitConverter.ToString(buffer, offset, elementSize),
            };
        }

        return values;
    }

    private static int GetPrimitiveByteSize(ushort typeCode)
    {
        return typeCode switch
        {
            BoolTypeCode => 1,
            SintTypeCode => 1,
            IntTypeCode => 2,
            DintTypeCode => 4,
            LintTypeCode => 8,
            RealTypeCode => 4,
            LrealTypeCode => 8,
            _ => 1,
        };
    }

    private static int GetElementCount(uint[]? dimensions)
    {
        if (dimensions is null || dimensions.Length == 0)
        {
            return 1;
        }

        var count = 1;

        foreach (var dimension in dimensions)
        {
            count *= (int)Math.Max(1, dimension);
        }

        return count;
    }

    private static string MapDataType(ushort typeCode)
    {
        return typeCode switch
        {
            BoolTypeCode => "bool",
            SintTypeCode => "sint",
            IntTypeCode => "int",
            DintTypeCode => "dint",
            LintTypeCode => "lint",
            RealTypeCode => "real",
            LrealTypeCode => "lreal",
            _ => $"type-{typeCode}",
        };
    }

    private static bool IsSupportedScalarType(ushort typeCode)
    {
        return typeCode is BoolTypeCode
            or SintTypeCode
            or IntTypeCode
            or DintTypeCode
            or LintTypeCode
            or RealTypeCode
            or LrealTypeCode;
    }

    private static bool IsScalarTag(TagInfo tag)
    {
        return tag.Dimensions is null || tag.Dimensions.Length == 0 || tag.Dimensions.All(dimension => dimension <= 1);
    }

    private static string? BuildParentPath(string tagName)
    {
        var separatorIndex = tagName.LastIndexOf('.');
        return separatorIndex > 0 ? tagName[..separatorIndex] : null;
    }
}