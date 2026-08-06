using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using backend.DTOs.Plc;
using backend.Interfaces.Plc;
using S7.Net;
using S7Plc = S7.Net.Plc;

namespace backend.Services.Plc;

public sealed class SiemensPlcDriver : IPlcDriver
{
    private enum DbBrowseMode
    {
        Compact,
        All,
        Bits,
        Bytes,
        Words,
        DWords,
    }

    private sealed record SiemensSchemaTagDefinition(
        string Name,
        string DisplayName,
        string DataType,
        string ParentPath,
        string? Description = null);

    private static readonly Regex DbStringAddressPattern = new(
        @"^DB(?<db>\d+)\.STRING(?<offset>\d+)\.(?<size>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DbBitAddressPattern = new(
        @"^DB(?<db>\d+)\.DBX(?<offset>\d+)\.(?<bit>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DbByteAddressPattern = new(
        @"^DB(?<db>\d+)\.DBB(?<offset>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DbWordAddressPattern = new(
        @"^DB(?<db>\d+)\.DBW(?<offset>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DbDWordAddressPattern = new(
        @"^DB(?<db>\d+)\.DBD(?<offset>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<int, IReadOnlyCollection<SiemensSchemaTagDefinition>> KnownDbSchemas
        = new Dictionary<int, IReadOnlyCollection<SiemensSchemaTagDefinition>>
        {
            [310] =
            [
                new("DB310.STRING0.256", "LINE_ID", "string", "DB310"),
                new("DB310.STRING256.256", "PRODUCT_ID", "string", "DB310"),
                new("DB310.DBX512.0", "LINE_RUNNING", "bool", "DB310", "Line Running 0=Down, 1=Running"),
                new("DB310.DBX512.1", "EXT_RUNNING", "bool", "DB310", "Extruder Running 0=Down, 1=Running"),
                new("DB310.DBX512.2", "LENGTH_RESET", "bool", "DB310", "Length Reset Pulse"),
                new("DB310.DBW514", "CONTROL_STATUS", "int", "DB310", "Diameter/Wall Control Status 0=Off, 1=Man, 2=Auto"),
                new("DB310.DBD516", "LINESPEED_SP", "real", "DB310", "Line speed Setpoint"),
                new("DB310.DBD520", "LINESPEED_ACT", "real", "DB310", "Line speed Actual"),
                new("DB310.DBD524", "EXT_SPEED_SP", "real", "DB310", "Extruder Speed Setpoint"),
                new("DB310.DBD528", "EXT_SPEED_ACT", "real", "DB310", "Extruder Speed Actual"),
                new("DB310.DBD532", "BARE_OD_SP", "real", "DB310", "Bare Diameter Setpoint"),
                new("DB310.DBD536", "BARE_OD_ACT", "real", "DB310", "Bare Diameter Actual"),
                new("DB310.DBD540", "HOT_OD_SP", "real", "DB310", "Hot Diameter Setpoint"),
                new("DB310.DBD544", "HOT_OD_ACT", "real", "DB310", "Hot Diameter Actual"),
                new("DB310.DBD548", "COLD_OD_SP", "real", "DB310", "Cold Diameter Setpoint"),
                new("DB310.DBD552", "COLD_OD_ACT", "real", "DB310", "Cold Diameter Actual"),
                new("DB310.DBD556", "HOT_WALL_SP", "real", "DB310", "Hot Wall Setpoint"),
                new("DB310.DBD560", "HOT_WALL_ACT", "real", "DB310", "Hot Wall Actual"),
                new("DB310.DBD564", "COLD_WALL_SP", "real", "DB310", "Cold Wall Setpoint"),
                new("DB310.DBD568", "COLD_WALL_ACT", "real", "DB310", "Cold Wall Actual"),
                new("DB310.DBD572", "BARE_OD_LIM", "real", "DB310", "Bare OD Limit"),
                new("DB310.DBD576", "HOT_OD_LIM", "real", "DB310", "Hot OD Limit"),
                new("DB310.DBD580", "COLD_OD_LIM", "real", "DB310", "Cold OD Limit"),
                new("DB310.DBD584", "HOT_WALL_LIM", "real", "DB310", "Hot Wall Limit"),
                new("DB310.DBD588", "COLD_WALL_LIM", "real", "DB310", "Cold Wall Limit"),
                new("DB310.DBW592", "SPARE_INT_5", "int", "DB310"),
                new("DB310.DBW594", "SPARE_INT_6", "int", "DB310"),
                new("DB310.DBD596", "SPARE_REAL_20", "real", "DB310"),
                new("DB310.DBD600", "SPARE_REAL_21", "real", "DB310"),
                new("DB310.DBD604", "SPARE_REAL_22", "real", "DB310"),
                new("DB310.DBX608.0", "LINE_DOWN", "bool", "DB310", "Line Down"),
                new("DB310.DBX608.1", "LINE_BLEEDOUT", "bool", "DB310", "Line Bleedout"),
                new("DB310.DBX608.2", "LINE_STARTUP", "bool", "DB310", "Line Startup"),
                new("DB310.DBX608.3", "LINE_PRODUCTION", "bool", "DB310", "Line Production"),
                new("DB310.DBX608.4", "LINESPEED_OK", "bool", "DB310", "Linespeed Ok"),
                new("DB310.DBX608.5", "EXTDRSPEED_OK", "bool", "DB310", "Extruder Speed Ok"),
                new("DB310.DBX608.6", "BARE_OD_OK", "bool", "DB310", "Bare OD Ok"),
                new("DB310.DBX608.7", "HOT_OD_OK", "bool", "DB310", "Hot OD Ok"),
                new("DB310.DBX609.0", "COLD_OD_OK", "bool", "DB310", "Cold OD Ok"),
                new("DB310.DBX609.1", "HOT_WALL_OK", "bool", "DB310", "Hot Wall Ok"),
                new("DB310.DBX609.2", "COLD_WALL_OK", "bool", "DB310", "Cold Wall Ok"),
                new("DB310.DBX609.3", "SPARE_BOOL_1", "bool", "DB310"),
                new("DB310.DBX609.4", "SPARE_BOOL_2", "bool", "DB310"),
                new("DB310.DBX609.5", "SPARE_BOOL_3", "bool", "DB310"),
                new("DB310.DBX609.6", "SPARE_BOOL_4", "bool", "DB310"),
                new("DB310.DBX609.7", "SPARE_BOOL_5", "bool", "DB310"),
                new("DB310.DBX610.0", "SPARE_BOOL_6", "bool", "DB310"),
                new("DB310.DBX610.1", "SPARE_BOOL_7", "bool", "DB310"),
                new("DB310.DBX610.2", "TEMP_BOOL_01", "bool", "DB310"),
                new("DB310.DBX610.3", "TEMP_BOOL_02", "bool", "DB310"),
                new("DB310.DBX610.4", "TEMP_BOOL_03", "bool", "DB310"),
                new("DB310.DBX610.5", "TEMP_BOOL_04", "bool", "DB310"),
                new("DB310.DBX610.6", "TEMP_BOOL_05", "bool", "DB310"),
                new("DB310.DBX610.7", "TEMP_BOOL_06", "bool", "DB310"),
                new("DB310.DBX611.0", "TEMP_BOOL_07", "bool", "DB310"),
                new("DB310.DBW612", "SPARE_INT_4", "int", "DB310"),
                new("DB310.DBW614", "MACHINE_STATE", "int", "DB310", "Machine State 0=Down, 1=Bleedout, 2=Startup, 3=Running"),
                new("DB310.DBW616", "TEMP_INT_01", "int", "DB310"),
                new("DB310.DBW618", "TEMP_INT_02", "int", "DB310"),
                new("DB310.DBW620", "TEMP_INT_3", "int", "DB310"),
                new("DB310.DBW622", "RET_VAL", "int", "DB310"),
                new("DB310.DBW624", "SPARE_INT_1", "int", "DB310"),
                new("DB310.DBW626", "SPARE_INT_2", "int", "DB310"),
                new("DB310.DBW628", "SPARE_INT_3", "int", "DB310"),
                new("DB310.DBD630", "SPARE_REAL_6", "real", "DB310"),
                new("DB310.DBD634", "SPARE_REAL_7", "real", "DB310"),
                new("DB310.DBD638", "LINESPEED_ERROR", "real", "DB310", "Linespeed Error (ACT-SP)"),
                new("DB310.DBD642", "LINESPEED_ERR_LIM", "real", "DB310", "Linespeed Error Limit"),
                new("DB310.DBD646", "SPARE_REAL_8", "real", "DB310"),
                new("DB310.DBD650", "SPARE_REAL_9", "real", "DB310"),
                new("DB310.DBD654", "EXTDRSPEED_ERROR", "real", "DB310", "Extruder Speed Error"),
                new("DB310.DBD658", "EXTDRSPEED_ERR_LIM", "real", "DB310", "Extruder Error Limit"),
                new("DB310.DBD662", "SPARE_REAL_10", "real", "DB310"),
                new("DB310.DBD666", "SPARE_REAL_11", "real", "DB310"),
                new("DB310.DBD670", "BARE_OD_ERR", "real", "DB310", "Bare Diameter Error"),
                new("DB310.DBD674", "BARE_TOL_ERR", "real", "DB310", "Bare OD Tolerance Error"),
                new("DB310.DBD678", "SPARE_REAL_12", "real", "DB310"),
                new("DB310.DBD682", "SPARE_REAL_13", "real", "DB310"),
                new("DB310.DBD686", "HOT_OD_ERR", "real", "DB310", "Hot Diameter Error"),
                new("DB310.DBD690", "HOT_TOL_ERR", "real", "DB310", "Hot OD Tolerance Error"),
                new("DB310.DBD694", "SPARE_REAL_14", "real", "DB310"),
                new("DB310.DBD698", "SPARE_REAL_15", "real", "DB310"),
                new("DB310.DBD702", "COLD_OD_ERR", "real", "DB310", "Cold Diameter Error"),
                new("DB310.DBD706", "COLD_TOL_ERR", "real", "DB310", "Cold OD Tolerance Error"),
                new("DB310.DBD710", "SPARE_REAL_16", "real", "DB310"),
                new("DB310.DBD714", "SPARE_REAL_17", "real", "DB310"),
                new("DB310.DBD718", "HOT_WALL_ERR", "real", "DB310", "Hot Wall Error"),
                new("DB310.DBD722", "HOT_WALL_TOL_ERR", "real", "DB310", "Hot Wall Tolerance Error"),
                new("DB310.DBD726", "SPARE_REAL_18", "real", "DB310"),
                new("DB310.DBD730", "SPARE_REAL_19", "real", "DB310"),
                new("DB310.DBD734", "COLD_WALL_ERR", "real", "DB310", "Cold Wall Error"),
                new("DB310.DBD738", "COLD_WALL_TOL_ERR", "real", "DB310", "Cold Wall Tolerance Error"),
                new("DB310.DBD742", "HOT_OD_VARIANCE", "real", "DB310", "Hot OD Variance = (HOT_SP-HOT_ACT)/HOT_SP"),
                new("DB310.DBD746", "COLD_OD_VARIANCE", "real", "DB310", "Cold OD Variance = (COLD_SP-COLD_ACT)/COLD_SP"),
                new("DB310.DBD750", "HOT_WALL_VARIANCE", "real", "DB310", "Hot Wall Variance = (HOT_SP-HOT_ACT)/HOT_SP"),
                new("DB310.DBD754", "COLD_WALL_VARIANCE", "real", "DB310", "Cold Wall Variance = (COLD_SP-COLD_ACT)/COLD_SP"),
                new("DB310.DBD758", "TEMP_REAL_1", "real", "DB310"),
                new("DB310.DBD762", "TEMP_REAL_2", "real", "DB310"),
                new("DB310.DBD766", "TEMP_REAL_3", "real", "DB310"),
                new("DB310.DBD770", "TEMP_REAL_4", "real", "DB310"),
                new("DB310.DBD774", "SPARE_REAL_1", "real", "DB310"),
                new("DB310.DBD778", "SPARE_REAL_2", "real", "DB310"),
                new("DB310.DBD782", "SPARE_REAL_3", "real", "DB310"),
                new("DB310.DBD786", "SPARE_REAL_4", "real", "DB310"),
                new("DB310.DBD790", "SPARE_REAL_5", "real", "DB310"),
            ],
        };

    private static readonly int[] DefaultDbNumbers = [1, 2];
    private const int DefaultRack = 0;
    private const int DefaultSlot = 1;
    private const int BrowseAddressLimit = 220;
    private const int MaxDbDiscoveryNumber = 512;
    private const int MaxDbCountPerRequest = 24;
    private const int MaxDbTagItemsPerRequest = 20000;
    private const int MaxDbBitExpansionBytes = 256;

    public string DriverName => "Siemens";

    public Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, PlcConnectionOptionsDto? options, CancellationToken cancellationToken = default)
    {
        return TestConnectionCoreAsync(ipAddress, options, cancellationToken);
    }

    public Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        return TestConnectionCoreAsync(ipAddress, null, cancellationToken);
    }

    public Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        return BrowseTagsCoreAsync(ipAddress, options, search, cancellationToken);
    }

    public Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsAsync(
        string ipAddress,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        return BrowseTagsCoreAsync(ipAddress, null, search, cancellationToken);
    }

    public Task<PlcTagReadResultDto> ReadTagAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        return ReadTagCoreAsync(ipAddress, options, tagName, cancellationToken);
    }

    public Task<PlcTagReadResultDto> ReadTagAsync(
        string ipAddress,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        return ReadTagCoreAsync(ipAddress, null, tagName, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PlcTagReadResultDto>> ReadTagsAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = NormalizeSettings(options);
        var uniqueNames = tagNames
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniqueNames.Count == 0)
        {
            return [];
        }

        try
        {
            using var plc = await OpenConnectedPlcAsync(ipAddress, settings, cancellationToken);
            var results = new List<PlcTagReadResultDto>(uniqueNames.Count);

            foreach (var name in uniqueNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await ReadSingleTagAsync(plc, name, settings.ReadTimeoutMs, cancellationToken));
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            return uniqueNames.Select(name => CreateReadFailure(name, "Timeout while reading PLC tags.")).ToList();
        }
        catch (Exception exception)
        {
            var message = MapMessage(exception);
            return uniqueNames.Select(name => CreateReadFailure(name, message)).ToList();
        }
    }

    public Task<IReadOnlyCollection<PlcTagReadResultDto>> ReadTagsAsync(
        string ipAddress,
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default)
    {
        return ReadTagsAsync(ipAddress, null, tagNames, cancellationToken);
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

    private async Task<PlcConnectionResult> TestConnectionCoreAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var settings = NormalizeSettings(options);

        try
        {
            using var plc = await OpenConnectedPlcAsync(ipAddress, settings, cancellationToken);

            stopwatch.Stop();
            return new PlcConnectionResult
            {
                IsConnected = plc.IsConnected,
                Driver = DriverName,
                IpAddress = ipAddress,
                ControllerName = settings.CpuType == CpuType.S71500
                    ? "Siemens S7-1500"
                    : "Siemens S7-1200 / S7-1217C",
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Message = "Connected to Siemens PLC.",
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateConnectionFailure(ipAddress, stopwatch, "Timeout while connecting to PLC.");
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return CreateConnectionFailure(ipAddress, stopwatch, MapMessage(exception));
        }
    }

    private async Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsCoreAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        string? search,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = NormalizeSettings(options);
        var normalizedSearch = search?.Trim();

        try
        {
            using var plc = await OpenConnectedPlcAsync(ipAddress, settings, cancellationToken);

            if (IsDbDiscoveryScope(normalizedSearch))
            {
                return await DiscoverDbFoldersAsync(plc, settings.ReadTimeoutMs, cancellationToken);
            }

            if (TryParseExplicitDbNumbers(normalizedSearch, out var explicitDbNumbers, out var dbBrowseMode))
            {
                return await BrowseDbAddressesAsync(plc, explicitDbNumbers, settings.ReadTimeoutMs, cancellationToken, dbBrowseMode);
            }

            var addresses = BuildBrowseCandidates(normalizedSearch)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(BrowseAddressLimit)
                .ToList();

            var results = new List<PlcTagBrowseItemDto>(addresses.Count);

            foreach (var address in addresses)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var value = await RunWithTimeoutAsync(
                        () => plc.Read(address),
                        settings.ReadTimeoutMs,
                        cancellationToken);

                    results.Add(new PlcTagBrowseItemDto
                    {
                        Name = address,
                        DataType = InferDataType(address, value),
                        IsFolder = false,
                        ParentPath = InferParentPath(address),
                        CanRead = true,
                        CanWrite = false,
                    });
                }
                catch
                {
                    // Browse probing skips non-readable/unmapped addresses.
                }
            }

            return results;
        }
        catch
        {
            return [];
        }
    }

    private static bool IsDbDiscoveryScope(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return false;
        }

        var normalized = search.Trim().ToUpperInvariant();
        return normalized is "DB" or "DBS" or "DBLIST" or "DATABASES";
    }

    private static bool TryParseExplicitDbNumbers(
        string? search,
        out IReadOnlyCollection<int> dbNumbers,
        out DbBrowseMode browseMode)
    {
        dbNumbers = [];
        browseMode = DbBrowseMode.Compact;
        if (string.IsNullOrWhiteSpace(search) || IsDbDiscoveryScope(search))
        {
            return false;
        }

        var trimmed = search.Trim();
        browseMode = ResolveDbBrowseMode(trimmed);
        var normalizedInput = Regex.Replace(
            trimmed,
            @"(?i)(:|\s+)(all|bits|bytes|words|dwords)$",
            string.Empty,
            RegexOptions.CultureInvariant)
            .Trim();

        var ids = new List<int>();

        if (int.TryParse(normalizedInput, out var directDb) && directDb > 0)
        {
            ids.Add(directDb);
        }

        var matches = Regex.Matches(normalizedInput, @"DB(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups["id"].Value, out var id) && id > 0)
            {
                ids.Add(id);
            }
        }

        if (ids.Count == 0)
        {
            var tokens = Regex.Split(normalizedInput, @"[^\d]+", RegexOptions.CultureInvariant);
            foreach (var token in tokens)
            {
                if (int.TryParse(token, out var id) && id > 0)
                {
                    ids.Add(id);
                }
            }
        }

        if (ids.Count == 0)
        {
            return false;
        }

        dbNumbers = ids.Distinct().Take(MaxDbCountPerRequest).ToList();
        return dbNumbers.Count > 0;
    }

    private static DbBrowseMode ResolveDbBrowseMode(string search)
    {
        var normalized = search.Trim().ToUpperInvariant();

        if (normalized.EndsWith(":ALL", StringComparison.Ordinal)
            || normalized.EndsWith(" ALL", StringComparison.Ordinal))
        {
            return DbBrowseMode.All;
        }

        if (normalized.EndsWith(":BITS", StringComparison.Ordinal)
            || normalized.EndsWith(" BITS", StringComparison.Ordinal))
        {
            return DbBrowseMode.Bits;
        }

        if (normalized.EndsWith(":BYTES", StringComparison.Ordinal)
            || normalized.EndsWith(" BYTES", StringComparison.Ordinal))
        {
            return DbBrowseMode.Bytes;
        }

        if (normalized.EndsWith(":WORDS", StringComparison.Ordinal)
            || normalized.EndsWith(" WORDS", StringComparison.Ordinal))
        {
            return DbBrowseMode.Words;
        }

        if (normalized.EndsWith(":DWORDS", StringComparison.Ordinal)
            || normalized.EndsWith(" DWORDS", StringComparison.Ordinal))
        {
            return DbBrowseMode.DWords;
        }

        return DbBrowseMode.Compact;
    }

    private static async Task<IReadOnlyCollection<PlcTagBrowseItemDto>> DiscoverDbFoldersAsync(
        S7Plc plc,
        int readTimeoutMs,
        CancellationToken cancellationToken)
    {
        var probeTimeoutMs = Math.Clamp(readTimeoutMs / 8, 120, 600);
        var folders = new List<PlcTagBrowseItemDto>();

        for (var dbNumber = 1; dbNumber <= MaxDbDiscoveryNumber; dbNumber += 1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await CanReadDataBlockByteAsync(plc, dbNumber, probeTimeoutMs, cancellationToken))
            {
                continue;
            }

            folders.Add(new PlcTagBrowseItemDto
            {
                Name = $"DB{dbNumber}",
                DataType = "folder",
                IsFolder = true,
                ParentPath = "DB",
                CanRead = false,
                CanWrite = false,
            });
        }

        return folders;
    }

    private static async Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseDbAddressesAsync(
        S7Plc plc,
        IReadOnlyCollection<int> dbNumbers,
        int readTimeoutMs,
        CancellationToken cancellationToken,
        DbBrowseMode browseMode)
    {
        var results = new List<PlcTagBrowseItemDto>();

        foreach (var dbNumber in dbNumbers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var schemaTags = TryBuildSchemaBrowseItems(dbNumber, browseMode);
            if (schemaTags is not null)
            {
                results.AddRange(schemaTags);
                continue;
            }

            var dbSizeBytes = await TryResolveDataBlockSizeBytesAsync(plc, dbNumber, readTimeoutMs, cancellationToken);
            if (dbSizeBytes is null || dbSizeBytes.Value <= 0)
            {
                continue;
            }

            var candidates = BuildDataBlockAddressCandidates(dbNumber, dbSizeBytes.Value, browseMode);
            foreach (var candidate in candidates)
            {
                results.Add(candidate);

                if (results.Count >= MaxDbTagItemsPerRequest)
                {
                    return results;
                }
            }
        }

        return results;
    }

    private static IReadOnlyCollection<PlcTagBrowseItemDto>? TryBuildSchemaBrowseItems(int dbNumber, DbBrowseMode browseMode)
    {
        if (browseMode != DbBrowseMode.Compact)
        {
            return null;
        }

        if (!KnownDbSchemas.TryGetValue(dbNumber, out var schema))
        {
            return null;
        }

        return schema
            .Select(definition => new PlcTagBrowseItemDto
            {
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                DataType = definition.DataType,
                IsFolder = false,
                ParentPath = definition.ParentPath,
                Description = definition.Description,
                CanRead = true,
                CanWrite = false,
            })
            .ToList();
    }

    private static IEnumerable<PlcTagBrowseItemDto> BuildDataBlockAddressCandidates(
        int dbNumber,
        int dbSizeBytes,
        DbBrowseMode browseMode)
    {
        var parentPath = $"DB{dbNumber}";

        if (browseMode is DbBrowseMode.Compact or DbBrowseMode.Words or DbBrowseMode.All)
        {
            for (var wordOffset = 0; wordOffset + 1 < dbSizeBytes; wordOffset += 2)
            {
                yield return CreateDbTagItem($"DB{dbNumber}.DBW{wordOffset}", "int", parentPath);
            }
        }

        if (browseMode is DbBrowseMode.Compact or DbBrowseMode.DWords or DbBrowseMode.All)
        {
            for (var dwordOffset = 0; dwordOffset + 3 < dbSizeBytes; dwordOffset += 4)
            {
                yield return CreateDbTagItem($"DB{dbNumber}.DBD{dwordOffset}", "dint", parentPath);
            }
        }

        if (browseMode is DbBrowseMode.Bytes or DbBrowseMode.All)
        {
            for (var byteOffset = 0; byteOffset < dbSizeBytes; byteOffset += 1)
            {
                yield return CreateDbTagItem($"DB{dbNumber}.DBB{byteOffset}", "int", parentPath);
            }
        }

        if (browseMode is DbBrowseMode.Bits or DbBrowseMode.All)
        {
            var bitExpansionBytes = Math.Min(dbSizeBytes, MaxDbBitExpansionBytes);
            for (var byteOffset = 0; byteOffset < bitExpansionBytes; byteOffset += 1)
            {
                for (var bit = 0; bit <= 7; bit += 1)
                {
                    yield return CreateDbTagItem($"DB{dbNumber}.DBX{byteOffset}.{bit}", "bool", parentPath);
                }
            }
        }
    }

    private static PlcTagBrowseItemDto CreateDbTagItem(string name, string dataType, string parentPath)
    {
        return new PlcTagBrowseItemDto
        {
            Name = name,
            DisplayName = null,
            DataType = dataType,
            IsFolder = false,
            ParentPath = parentPath,
            Description = null,
            CanRead = true,
            CanWrite = false,
        };
    }

    private static async Task<bool> CanReadDataBlockByteAsync(
        S7Plc plc,
        int dbNumber,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await RunWithTimeoutAsync(
                () => plc.ReadBytes(S7.Net.DataType.DataBlock, dbNumber, 0, 1),
                timeoutMs,
                cancellationToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<int?> TryResolveDataBlockSizeBytesAsync(
        S7Plc plc,
        int dbNumber,
        int readTimeoutMs,
        CancellationToken cancellationToken)
    {
        if (!await CanReadDataBlockByteAsync(plc, dbNumber, readTimeoutMs, cancellationToken))
        {
            return null;
        }

        var low = 1;
        var high = 1;
        const int maxProbeSize = 65536;

        while (high < maxProbeSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var next = Math.Min(high * 2, maxProbeSize);
            if (!await TryReadDataBlockAsync(plc, dbNumber, next, readTimeoutMs, cancellationToken))
            {
                high = next;
                break;
            }

            low = next;
            high = next;
        }

        if (high == low)
        {
            return low;
        }

        var left = low;
        var right = high;

        while (left + 1 < right)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var middle = left + ((right - left) / 2);
            if (await TryReadDataBlockAsync(plc, dbNumber, middle, readTimeoutMs, cancellationToken))
            {
                left = middle;
            }
            else
            {
                right = middle;
            }
        }

        return left;
    }

    private static async Task<bool> TryReadDataBlockAsync(
        S7Plc plc,
        int dbNumber,
        int byteCount,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await RunWithTimeoutAsync(
                () => plc.ReadBytes(S7.Net.DataType.DataBlock, dbNumber, 0, byteCount),
                timeoutMs,
                cancellationToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<PlcTagReadResultDto> ReadTagCoreAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        string tagName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = NormalizeSettings(options);
        var normalizedTag = tagName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedTag))
        {
            return CreateReadFailure(tagName, "Tag name is required.");
        }

        try
        {
            using var plc = await OpenConnectedPlcAsync(ipAddress, settings, cancellationToken);
            return await ReadSingleTagAsync(plc, normalizedTag, settings.ReadTimeoutMs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return CreateReadFailure(normalizedTag, "Timeout while reading PLC tag.");
        }
        catch (Exception exception)
        {
            return CreateReadFailure(normalizedTag, MapMessage(exception));
        }
    }

    private static async Task<PlcTagReadResultDto> ReadSingleTagAsync(
        S7Plc plc,
        string tagName,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            if (TryParseCustomStringTag(tagName, out var dbNumber, out var byteOffset, out var byteCount))
            {
                var bytes = await RunWithTimeoutAsync(
                    () => plc.ReadBytes(S7.Net.DataType.DataBlock, dbNumber, byteOffset, byteCount),
                    timeoutMs,
                    cancellationToken);

                return new PlcTagReadResultDto
                {
                    Name = tagName,
                    DataType = "string",
                    Value = DecodeSiemensString(bytes),
                    LastReadUtc = DateTime.UtcNow,
                    CanRead = true,
                    CanWrite = false,
                };
            }

            if (TryResolveSchemaDefinition(tagName, out var schemaDefinition))
            {
                var typedResult = await TryReadSchemaTypedTagAsync(plc, tagName, schemaDefinition.DataType, timeoutMs, cancellationToken);
                if (typedResult is not null)
                {
                    return typedResult;
                }
            }

            var value = await RunWithTimeoutAsync(() => plc.Read(tagName), timeoutMs, cancellationToken);

            return new PlcTagReadResultDto
            {
                Name = tagName,
                DataType = InferDataType(tagName, value),
                Value = ConvertToString(value),
                LastReadUtc = DateTime.UtcNow,
                CanRead = true,
                CanWrite = false,
            };
        }
        catch (Exception exception)
        {
            return CreateReadFailure(tagName, MapMessage(exception));
        }
    }

    private static bool TryResolveSchemaDefinition(string tagName, out SiemensSchemaTagDefinition definition)
    {
        foreach (var schema in KnownDbSchemas.Values)
        {
            var match = schema.FirstOrDefault(item => item.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                definition = match;
                return true;
            }
        }

        definition = default!;
        return false;
    }

    private static async Task<PlcTagReadResultDto?> TryReadSchemaTypedTagAsync(
        S7Plc plc,
        string tagName,
        string dataType,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        switch (dataType.ToLowerInvariant())
        {
            case "bool":
                if (TryParseDbBitAddress(tagName, out var boolDb, out var boolOffset, out var bitIndex))
                {
                    var boolBytes = await RunWithTimeoutAsync(
                        () => plc.ReadBytes(S7.Net.DataType.DataBlock, boolDb, boolOffset, 1),
                        timeoutMs,
                        cancellationToken);

                    var value = (boolBytes[0] & (1 << bitIndex)) != 0;
                    return CreateTypedReadResult(tagName, "bool", value);
                }

                break;

            case "int":
                if (TryParseDbWordAddress(tagName, out var intDb, out var intOffset))
                {
                    var intBytes = await RunWithTimeoutAsync(
                        () => plc.ReadBytes(S7.Net.DataType.DataBlock, intDb, intOffset, 2),
                        timeoutMs,
                        cancellationToken);

                    var value = ReadInt16BigEndian(intBytes);
                    return CreateTypedReadResult(tagName, "int", value);
                }

                if (TryParseDbByteAddress(tagName, out var byteDb, out var byteOffsetValue))
                {
                    var byteBytes = await RunWithTimeoutAsync(
                        () => plc.ReadBytes(S7.Net.DataType.DataBlock, byteDb, byteOffsetValue, 1),
                        timeoutMs,
                        cancellationToken);

                    return CreateTypedReadResult(tagName, "int", (short)byteBytes[0]);
                }

                break;

            case "dint":
                if (TryParseDbDWordAddress(tagName, out var dintDb, out var dintOffset))
                {
                    var dintBytes = await RunWithTimeoutAsync(
                        () => plc.ReadBytes(S7.Net.DataType.DataBlock, dintDb, dintOffset, 4),
                        timeoutMs,
                        cancellationToken);

                    var value = ReadInt32BigEndian(dintBytes);
                    return CreateTypedReadResult(tagName, "dint", value);
                }

                break;

            case "real":
                if (TryParseDbDWordAddress(tagName, out var realDb, out var realOffset))
                {
                    var realBytes = await RunWithTimeoutAsync(
                        () => plc.ReadBytes(S7.Net.DataType.DataBlock, realDb, realOffset, 4),
                        timeoutMs,
                        cancellationToken);

                    var value = ReadSingleBigEndian(realBytes);
                    return CreateTypedReadResult(tagName, "real", value);
                }

                break;
        }

        return null;
    }

    private static PlcTagReadResultDto CreateTypedReadResult(string tagName, string dataType, object value)
    {
        return new PlcTagReadResultDto
        {
            Name = tagName,
            DataType = dataType,
            Value = ConvertToString(value),
            LastReadUtc = DateTime.UtcNow,
            CanRead = true,
            CanWrite = false,
        };
    }

    private static bool TryParseDbBitAddress(string tagName, out int dbNumber, out int byteOffset, out int bitIndex)
    {
        dbNumber = 0;
        byteOffset = 0;
        bitIndex = 0;
        var match = DbBitAddressPattern.Match(tagName);
        return match.Success
            && int.TryParse(match.Groups["db"].Value, out dbNumber)
            && int.TryParse(match.Groups["offset"].Value, out byteOffset)
            && int.TryParse(match.Groups["bit"].Value, out bitIndex)
            && bitIndex is >= 0 and <= 7;
    }

    private static bool TryParseDbByteAddress(string tagName, out int dbNumber, out int byteOffset)
    {
        dbNumber = 0;
        byteOffset = 0;
        var match = DbByteAddressPattern.Match(tagName);
        return match.Success
            && int.TryParse(match.Groups["db"].Value, out dbNumber)
            && int.TryParse(match.Groups["offset"].Value, out byteOffset);
    }

    private static bool TryParseDbWordAddress(string tagName, out int dbNumber, out int byteOffset)
    {
        dbNumber = 0;
        byteOffset = 0;
        var match = DbWordAddressPattern.Match(tagName);
        return match.Success
            && int.TryParse(match.Groups["db"].Value, out dbNumber)
            && int.TryParse(match.Groups["offset"].Value, out byteOffset);
    }

    private static bool TryParseDbDWordAddress(string tagName, out int dbNumber, out int byteOffset)
    {
        dbNumber = 0;
        byteOffset = 0;
        var match = DbDWordAddressPattern.Match(tagName);
        return match.Success
            && int.TryParse(match.Groups["db"].Value, out dbNumber)
            && int.TryParse(match.Groups["offset"].Value, out byteOffset);
    }

    private static short ReadInt16BigEndian(byte[] bytes)
    {
        return (short)((bytes[0] << 8) | bytes[1]);
    }

    private static int ReadInt32BigEndian(byte[] bytes)
    {
        return (bytes[0] << 24)
            | (bytes[1] << 16)
            | (bytes[2] << 8)
            | bytes[3];
    }

    private static float ReadSingleBigEndian(byte[] bytes)
    {
        var buffer = (byte[])bytes.Clone();
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(buffer);
        }

        return BitConverter.ToSingle(buffer, 0);
    }

    private static bool TryParseCustomStringTag(string tagName, out int dbNumber, out int byteOffset, out int byteCount)
    {
        dbNumber = 0;
        byteOffset = 0;
        byteCount = 0;

        var match = DbStringAddressPattern.Match(tagName);
        if (!match.Success)
        {
            return false;
        }

        return int.TryParse(match.Groups["db"].Value, out dbNumber)
            && int.TryParse(match.Groups["offset"].Value, out byteOffset)
            && int.TryParse(match.Groups["size"].Value, out byteCount)
            && dbNumber > 0
            && byteOffset >= 0
            && byteCount >= 2;
    }

    private static string DecodeSiemensString(byte[] bytes)
    {
        if (bytes.Length < 2)
        {
            return string.Empty;
        }

        var maxLength = bytes[0];
        var actualLength = Math.Min(bytes[1], Math.Min(maxLength, bytes.Length - 2));
        if (actualLength <= 0)
        {
            return string.Empty;
        }

        return Encoding.ASCII.GetString(bytes, 2, actualLength).TrimEnd('\0', ' ');
    }

    private static PlcTagReadResultDto CreateReadFailure(string? tagName, string error)
    {
        return new PlcTagReadResultDto
        {
            Name = tagName ?? string.Empty,
            LastReadUtc = DateTime.UtcNow,
            CanRead = false,
            CanWrite = false,
            Error = error,
        };
    }

    private static PlcConnectionResult CreateConnectionFailure(string ipAddress, System.Diagnostics.Stopwatch stopwatch, string message)
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

    private static async Task<S7Plc> OpenConnectedPlcAsync(
        string ipAddress,
        SiemensConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        var plc = new S7Plc(settings.CpuType, ipAddress.Trim(), (short)settings.Rack, (short)settings.Slot);

        try
        {
            await RunWithTimeoutAsync(
                () =>
                {
                    plc.Open();
                    return true;
                },
                settings.ConnectionTimeoutMs,
                cancellationToken);

            if (!plc.IsConnected)
            {
                throw new PlcException(ErrorCode.ConnectionError, "PLC did not report an active connection.");
            }

            return plc;
        }
        catch
        {
            plc.Close();
            throw;
        }
    }

    private static IEnumerable<string> BuildBrowseCandidates(string? search)
    {
        if (TryParseBitAreaSearch(search, out var area))
        {
            foreach (var candidate in BuildBitAreaCandidates(area))
            {
                yield return candidate;
            }

            yield break;
        }

        var dbNumbers = ParseDbNumbersFromSearch(search);

        foreach (var db in dbNumbers)
        {
            for (var offset = 0; offset <= 64; offset += 2)
            {
                yield return $"DB{db}.DBW{offset}";
            }

            for (var offset = 0; offset <= 64; offset += 4)
            {
                yield return $"DB{db}.DBD{offset}";
            }

            for (var byteOffset = 0; byteOffset <= 7; byteOffset += 1)
            {
                for (var bit = 0; bit <= 7; bit += 1)
                {
                    yield return $"DB{db}.DBX{byteOffset}.{bit}";
                }
            }
        }

        foreach (var areaCode in new[] { "M", "I", "Q" })
        {
            foreach (var candidate in BuildBitAreaCandidates(areaCode))
            {
                yield return candidate;
            }
        }
    }

    private static bool TryParseBitAreaSearch(string? search, out string area)
    {
        area = string.Empty;
        if (string.IsNullOrWhiteSpace(search))
        {
            return false;
        }

        var trimmed = search.Trim().ToUpperInvariant();
        if (trimmed is "M" or "I" or "Q")
        {
            area = trimmed;
            return true;
        }

        return false;
    }

    private static IEnumerable<string> BuildBitAreaCandidates(string area)
    {
        for (var byteOffset = 0; byteOffset <= 15; byteOffset += 1)
        {
            for (var bit = 0; bit <= 7; bit += 1)
            {
                yield return $"{area}{byteOffset}.{bit}";
            }
        }
    }

    private static IReadOnlyCollection<int> ParseDbNumbersFromSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return DefaultDbNumbers;
        }

        var trimmed = search.Trim();
        if (int.TryParse(trimmed, out var directDb) && directDb > 0)
        {
            return [directDb];
        }

        var matches = Regex.Matches(search, @"DB(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var ids = new List<int>();
        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups["id"].Value, out var id) && id > 0)
            {
                ids.Add(id);
            }
        }

        if (ids.Count == 0)
        {
            // Also support comma/space separated values like "1,2,10".
            var tokens = Regex.Split(trimmed, @"[^\d]+", RegexOptions.CultureInvariant);
            foreach (var token in tokens)
            {
                if (int.TryParse(token, out var id) && id > 0)
                {
                    ids.Add(id);
                }
            }
        }

        return ids.Count == 0
            ? DefaultDbNumbers
            : ids.Distinct().Take(4).ToList();
    }

    private static string InferDataType(string address, object? value)
    {
        if (value is bool)
        {
            return "bool";
        }

        if (value is byte or short or ushort)
        {
            return "int";
        }

        if (value is int or uint)
        {
            return "dint";
        }

        if (value is long or ulong)
        {
            return "lint";
        }

        if (value is float)
        {
            return "real";
        }

        if (value is double or decimal)
        {
            return "lreal";
        }

        if (value is string)
        {
            return "string";
        }

        if (address.Contains("DBX", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(address, "^[MIQ]\\d+\\.\\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return "bool";
        }

        if (address.Contains("DBD", StringComparison.OrdinalIgnoreCase))
        {
            return "dint";
        }

        if (address.Contains("DBW", StringComparison.OrdinalIgnoreCase))
        {
            return "int";
        }

        if (address.Contains("DBB", StringComparison.OrdinalIgnoreCase))
        {
            return "int";
        }

        return "unknown";
    }

    private static string? InferParentPath(string address)
    {
        var dbMatch = Regex.Match(address, @"^(DB\d+)\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (dbMatch.Success)
        {
            return dbMatch.Groups[1].Value.ToUpperInvariant();
        }

        if (address.StartsWith("M", StringComparison.OrdinalIgnoreCase))
        {
            return "M";
        }

        if (address.StartsWith("I", StringComparison.OrdinalIgnoreCase))
        {
            return "I";
        }

        if (address.StartsWith("Q", StringComparison.OrdinalIgnoreCase))
        {
            return "Q";
        }

        return null;
    }

    private static string? ConvertToString(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            bool b => b ? "1" : "0",
            float f => f.ToString("G9", CultureInfo.InvariantCulture),
            double d => d.ToString("G17", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }

    private static string MapMessage(Exception exception)
    {
        var message = exception.Message.ToLowerInvariant();

        if (message.Contains("timeout"))
        {
            return "Timeout while communicating with Siemens PLC.";
        }

        if (message.Contains("refused") || message.Contains("unreachable") || message.Contains("connectionerror"))
        {
            return "PLC offline, network unreachable, or firewall blocked the connection.";
        }

        if (message.Contains("rack") || message.Contains("slot") || message.Contains("cpu"))
        {
            return "Invalid Siemens CPU, rack, or slot configuration.";
        }

        return $"Communication exception while connecting to PLC: {exception.Message}";
    }

    private static SiemensConnectionSettings NormalizeSettings(PlcConnectionOptionsDto? options)
    {
        var processorType = (options?.ProcessorType ?? "S7-1217C").Trim();
        var routePath = options?.RoutePath?.Trim();

        var rack = options?.Rack;
        var slot = options?.Slot;

        if ((!rack.HasValue || !slot.HasValue) && TryParseRackSlot(routePath, out var parsedRack, out var parsedSlot))
        {
            rack = parsedRack;
            slot = parsedSlot;
        }

        return new SiemensConnectionSettings
        {
            CpuType = ResolveCpuType(processorType),
            ProcessorType = NormalizeProcessorType(processorType),
            Rack = rack ?? DefaultRack,
            Slot = slot ?? DefaultSlot,
            ConnectionTimeoutMs = ClampTimeout(options?.ConnectionTimeoutMs, 3000),
            ReadTimeoutMs = ClampTimeout(options?.ReadTimeoutMs, 3000),
        };
    }

    private static bool TryParseRackSlot(string? routePath, out int rack, out int slot)
    {
        rack = DefaultRack;
        slot = DefaultSlot;

        if (string.IsNullOrWhiteSpace(routePath))
        {
            return false;
        }

        var segments = routePath.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        return int.TryParse(segments[^2], out rack) && int.TryParse(segments[^1], out slot);
    }

    private static CpuType ResolveCpuType(string processorType)
    {
        var normalized = processorType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "s7-1500" or "s71500" => CpuType.S71500,
            "s7-1200" or "s71200" or "s7-1217c" => CpuType.S71200,
            _ => CpuType.S71200,
        };
    }

    private static string NormalizeProcessorType(string processorType)
    {
        var normalized = processorType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "s7-1500" or "s71500" => "S7-1500",
            "s7-1200" or "s71200" => "S7-1200",
            _ => "S7-1217C",
        };
    }

    private static int ClampTimeout(int? value, int fallback)
    {
        var candidate = value.GetValueOrDefault(fallback);
        if (candidate < 500)
        {
            return 500;
        }

        if (candidate > 30000)
        {
            return 30000;
        }

        return candidate;
    }

    private static async Task<T> RunWithTimeoutAsync<T>(Func<T> operation, int timeoutMs, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        return await Task.Run(operation, timeoutCts.Token);
    }

    private sealed record SiemensConnectionSettings
    {
        public CpuType CpuType { get; init; }
        public string ProcessorType { get; init; } = "S7-1217C";
        public int Rack { get; init; } = DefaultRack;
        public int Slot { get; init; } = DefaultSlot;
        public int ConnectionTimeoutMs { get; init; } = 3000;
        public int ReadTimeoutMs { get; init; } = 3000;
    }
}
