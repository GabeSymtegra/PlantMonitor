using backend.DTOs.Runtime;
using backend.DTOs.Plc;
using backend.DTOs.Production;
using backend.Data;
using backend.Interfaces.Production;
using backend.Interfaces.Plc;
using backend.Models.Production;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Production;

// This service is the runtime source of truth for live dashboard state,
// line detail snapshots, and live PLC polling.
public sealed class ProductionRuntimeService : BackgroundService, IProductionRuntimeService
{
    // Shared mutable runtime state for all configured lines.
    private readonly object _gate = new();
    private readonly Dictionary<int, LineRuntimeState> _lines;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPlcConnectionService _plcConnectionService;
    private readonly ILogger<ProductionRuntimeService> _logger;
    private static readonly TimeSpan AssignmentRefreshInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ActiveRuntimePersistenceInterval = TimeSpan.FromSeconds(5);
    private const int MaxPollConcurrency = 4;

    private const string LogicalKeyControlMode = "control_mode";
    private const string LogicalKeyMachineState = "machine_state";
    private const string LogicalKeyProductionLength = "production_length";
    private const string LogicalKeyProductId = "product_id";
    private const string LogicalKeyBareSetpoint = "bare_setpoint";
    private const string LogicalKeyBareActual = "bare_actual";
    private const string LogicalKeyHotSetpoint = "hot_setpoint";
    private const string LogicalKeyHotActual = "hot_actual";
    private const string LogicalKeyColdSetpoint = "cold_setpoint";
    private const string LogicalKeyColdActual = "cold_actual";

    // -------------------------------------------------------------------------
    // Construction and query APIs
    // -------------------------------------------------------------------------

    public ProductionRuntimeService(
        ILogger<ProductionRuntimeService> logger,
        IServiceScopeFactory scopeFactory,
        IPlcConnectionService plcConnectionService)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _plcConnectionService = plcConnectionService;
        _lines = BuildLineStates();
    }

    // Dashboard uses a flattened line snapshot for the main overview page.
    public DashboardSnapshotDto GetDashboardSnapshot()
    {
        lock (_gate)
        {
            var lines = _lines.Values
                .Select(ToDashboardLine)
                .OrderBy(line => line.LineNumber)
                .ToList();

            var lastUpdated = lines.Count == 0
                ? DateTime.UtcNow
                : lines.Max(line => line.UpdatedAtUtc);

            return new DashboardSnapshotDto
            {
                Lines = lines,
                LastUpdatedUtc = lastUpdated,
            };
        }
    }

    public bool IsKnownLine(int lineId)
    {
        lock (_gate)
        {
            return _lines.ContainsKey(lineId);
        }
    }

    // Line details expose the deeper runtime breakdown shown on the line page.
    public LineDetailSnapshotDto? GetLineDetail(int lineId)
    {
        lock (_gate)
        {
            if (!_lines.TryGetValue(lineId, out var state))
            {
                return null;
            }

            var snapshot = GetSnapshotOrFallback(state);
            var runtimeSeconds = Math.Max(0L, (long)(snapshot.LastUpdateUtc - snapshot.Metadata.StartTimeUtc).TotalSeconds);

            var sensors = Enum.GetValues<MeasurementZone>()
                .Select(zone =>
                {
                    var live = snapshot.LiveZones[zone];
                    var overall = snapshot.OverallZones[zone];
                    var auto = snapshot.AutoQuality.Zones[zone];
                    var manual = snapshot.ManualQuality.Zones[zone];

                    return new SensorSectionDto
                    {
                        Zone = ZoneToLabel(zone),
                        CurrentSetpoint = live.CurrentSetpoint,
                        CurrentActual = live.CurrentActual,
                        CurrentPercentDeviation = live.CurrentPercentDeviation,
                        OverallMeasurementCount = overall.MeasurementCount,
                        OverallAverageAbsoluteDeviation = overall.AverageAbsoluteDeviation,
                        OverallMaxPositiveDeviation = overall.MaxPositiveDeviation,
                        OverallMaxNegativeDeviation = overall.MaxNegativeDeviation,
                        AutoMeasurementCount = auto.MeasurementCount,
                        AutoAverageAbsoluteDeviation = auto.AverageAbsoluteDeviation,
                        AutoMaxPositiveDeviation = auto.MaxPositiveDeviation,
                        AutoMaxNegativeDeviation = auto.MaxNegativeDeviation,
                        ManualMeasurementCount = manual.MeasurementCount,
                        ManualAverageAbsoluteDeviation = manual.AverageAbsoluteDeviation,
                        ManualMaxPositiveDeviation = manual.MaxPositiveDeviation,
                        ManualMaxNegativeDeviation = manual.MaxNegativeDeviation,
                    };
                })
                .ToList();

            return new LineDetailSnapshotDto
            {
                Id = state.LineId,
                LineNumber = state.LineNumber,
                LineName = state.LineName,
                ProductId = NormalizeProductIdForDisplay(state.CurrentProductId),
                RecipeId = state.Metadata.RecipeId,
                MachineId = state.Metadata.MachineId,
                OperatorName = state.Metadata.OperatorName,
                PlcIp = state.PlcIp,
                Manufacturer = state.Manufacturer,
                Status = state.Status,
                ControlMode = snapshot.CurrentMode.ToString(),
                LastUpdatedUtc = snapshot.LastUpdateUtc,
                StartTimeUtc = state.Metadata.StartTimeUtc,
                CurrentProductionLength = snapshot.CurrentProductionLength,
                RuntimeSeconds = runtimeSeconds,
                AutoTimeSeconds = snapshot.AutoTimeSeconds,
                ManualTimeSeconds = snapshot.ManualTimeSeconds,
                AutoPercentage = snapshot.AutoPercentage,
                ManualPercentage = snapshot.ManualPercentage,
                Sensors = sensors,
            };
        }
    }

    // -------------------------------------------------------------------------
    // Background polling loop
    // -------------------------------------------------------------------------

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var nextAssignmentRefresh = DateTime.UtcNow;

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var now = DateTime.UtcNow;

                try
                {
                    if (now >= nextAssignmentRefresh)
                    {
                        RefreshLineStatesFromAssignments(now);
                        nextAssignmentRefresh = now.Add(AssignmentRefreshInterval);
                    }

                    List<LineRuntimeState> dueLines;
                    lock (_gate)
                    {
                        dueLines = _lines.Values
                            .Where(state => state.IsEnabled && now >= state.NextPollAtUtc && state.TryBeginTick())
                            .ToList();
                    }

                    if (dueLines.Count == 0)
                    {
                        continue;
                    }

                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Clamp(dueLines.Count, 1, MaxPollConcurrency),
                        CancellationToken = stoppingToken,
                    };

                    await Parallel.ForEachAsync(dueLines, parallelOptions, async (state, cancellationToken) =>
                    {
                        try
                        {
                            await PollLineAsync(state, now, cancellationToken);
                        }
                        finally
                        {
                            state.CompleteTick(now);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Runtime polling iteration failed and will be retried on the next tick.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Production runtime service stopping.");
        }
    }

    private async Task PollLineAsync(LineRuntimeState state, DateTime now, CancellationToken stoppingToken)
    {
        var updatedFromPlc = false;

        try
        {
            // A successful mapped tick means the line snapshot came
            // from a live PLC read rather than a fallback path.
            updatedFromPlc = await TryTickStateFromMappedPlcAsync(state, now, stoppingToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Mapped PLC tick failed for line {LineId}. Marking line Offline.",
                state.LineId);
        }

        if (!updatedFromPlc)
        {
            // When the PLC cannot be reached, surface that directly
            // instead of inventing simulated production behavior.
            await MarkLineOfflineAsync(state, now, stoppingToken);
        }

        await HandleRunLifecycleTransitionAsync(state, now, stoppingToken);
        await PersistActiveRuntimeStateAsync(state, now, stoppingToken);
    }

    private void RefreshLineStatesFromAssignments(DateTime now)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();

        var activeAssignments = dbContext.LineProtocolAssignments
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToList();

        var persistedRuntimeStates = dbContext.ActiveLineRuntimeStates
            .AsNoTracking()
            .ToDictionary(x => x.LineId);

        var activeLineIds = activeAssignments
            .Select(x => x.LineId)
            .ToHashSet();

        lock (_gate)
        {
            foreach (var assignment in activeAssignments)
            {
                if (!_lines.TryGetValue(assignment.LineId, out var state))
                {
                    state = new LineRuntimeState(
                        lineId: assignment.LineId,
                        lineNumber: assignment.LineNumber,
                        lineName: assignment.LineName,
                        plcIp: assignment.PlcIp,
                        manufacturer: assignment.Manufacturer,
                        pollIntervalMs: assignment.PollIntervalMs,
                        metadata: BuildRunMetadata(
                            assignment,
                            assignment.ProductId,
                            now));

                    if (persistedRuntimeStates.TryGetValue(assignment.LineId, out var checkpoint))
                    {
                        state.ApplyPersistedCheckpoint(checkpoint);
                    }

                    _lines[assignment.LineId] = state;
                }

                state.LineNumber = assignment.LineNumber;
                state.LineName = assignment.LineName;
                state.PlcIp = assignment.PlcIp;
                state.Manufacturer = assignment.Manufacturer;
                state.PollIntervalMs = Math.Clamp(assignment.PollIntervalMs, 500, 60000);
                state.IsEnabled = true;

                var productId = string.IsNullOrWhiteSpace(state.CurrentProductId)
                    ? assignment.ProductId
                    : state.CurrentProductId;

                state.CurrentProductId = NormalizeProductId(productId);
                state.Metadata = BuildRunMetadata(assignment, state.CurrentProductId, state.Metadata.StartTimeUtc);
            }

            foreach (var state in _lines.Values)
            {
                if (!activeLineIds.Contains(state.LineId))
                {
                    state.IsEnabled = false;
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Live PLC tick handling
    // -------------------------------------------------------------------------

    private async Task<bool> TryTickStateFromMappedPlcAsync(
        LineRuntimeState state,
        DateTime now,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IPlcProtocolConfigService>();
        var catalog = configService.GetTagCatalog(state.LineId)
            .Where(x => x.IsEnabled)
            .ToList();

        if (catalog.Count == 0)
        {
            return false;
        }

        if (!_plcConnectionService.TryResolveDriver(state.Manufacturer, out var driver, out _)
            || driver is null)
        {
            return false;
        }

        var addresses = catalog
            .Select(x => x.PlcAddress)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (addresses.Count == 0)
        {
            return false;
        }

        var readResults = await driver.ReadTagsAsync(state.PlcIp, addresses, cancellationToken);
        var readMap = readResults
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToDictionary(x => x.Name.Trim(), x => x, StringComparer.OrdinalIgnoreCase);

        var catalogMap = catalog.ToDictionary(x => x.LogicalKey, StringComparer.OrdinalIgnoreCase);

        if (!TryGetZoneSample(catalogMap, readMap, LogicalKeyBareSetpoint, LogicalKeyBareActual, out var bare)
            || !TryGetZoneSample(catalogMap, readMap, LogicalKeyHotSetpoint, LogicalKeyHotActual, out var hot)
            || !TryGetZoneSample(catalogMap, readMap, LogicalKeyColdSetpoint, LogicalKeyColdActual, out var cold))
        {
            return false;
        }

        var controlMode = TryGetControlMode(catalogMap, readMap, out var mode) ? mode : ControlMode.Auto;

        var productionLength = TryGetScaledNumberByLogicalKey(
            catalogMap,
            readMap,
            LogicalKeyProductionLength,
            out var mappedLength)
            ? mappedLength
            : state.ProductionLength;

        if (TryGetReadableProductId(catalogMap, readMap, out var product)
            && !string.IsNullOrWhiteSpace(product))
        {
            state.CurrentProductId = NormalizeProductId(product);
        }

        var nextStatus = TryResolveMachineState(catalogMap, readMap, out var machineStatus)
            ? machineStatus
            : "Running";

        string previousStatus;
        string currentStatus;
        ControlMode previousMode;
        ControlMode currentMode;

        lock (_gate)
        {
            previousStatus = state.Status;
            previousMode = state.LastKnownControlMode;
            UpdateStatus(state, nextStatus, now);
            state.LastKnownControlMode = controlMode;
            state.LastTickUtc = now;
            state.ProductionLength = productionLength;
            currentStatus = state.Status;
            currentMode = state.LastKnownControlMode;
        }

        if (!string.Equals(previousStatus, currentStatus, StringComparison.OrdinalIgnoreCase))
        {
            await PersistRuntimeEventAsync(state, "StatusSwitch", previousStatus, currentStatus, now, cancellationToken);
        }

        if (previousMode != currentMode)
        {
            await PersistRuntimeEventAsync(state, "ModeSwitch", previousMode.ToString(), currentMode.ToString(), now, cancellationToken);
        }

        if (IsStopState(state.Status))
        {
            return true;
        }

        EnsureRunIsActiveForSampling(state, now);

        lock (_gate)
        {
            state.Engine.ApplySample(new ProductionTelemetrySample(
                TimestampUtc: now,
                Mode: controlMode,
                ProductionLength: productionLength,
                Zones: new Dictionary<MeasurementZone, ZoneSample>
                {
                    [MeasurementZone.BareOd] = bare,
                    [MeasurementZone.HotOd] = hot,
                    [MeasurementZone.ColdOd] = cold,
                }));
        }

        return true;
    }

    // Sampling must only happen while a production run is active in the
    // statistics engine. This method restarts the engine on the first live tick
    // after a stop or offline period.
    private void EnsureRunIsActiveForSampling(LineRuntimeState state, DateTime now)
    {
        if (state.Engine.IsActive)
        {
            return;
        }

        lock (_gate)
        {
            if (state.Engine.IsActive)
            {
                return;
            }

            var nextMetadata = state.Metadata with
            {
                ProductId = NormalizeProductId(state.CurrentProductId),
                StartTimeUtc = now,
            };

            state.Metadata = nextMetadata;
            state.ProductionLength = 0d;
            state.HasPersistedCurrentStop = false;
            state.HasSeenRunningState = false;
            state.Engine.StartRun(nextMetadata);
            state.LastTickUtc = now;
            state.StatusEnteredUtc = now;
        }
    }

    // Initial line state is reconstructed from configured lines in the database.
    private Dictionary<int, LineRuntimeState> BuildLineStates()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();

        var configuredLines = dbContext.LineProtocolAssignments
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.LineNumber)
            .ToList();

        var persistedRuntimeStates = dbContext.ActiveLineRuntimeStates
            .AsNoTracking()
            .ToDictionary(x => x.LineId);

        var now = DateTime.UtcNow;

        var lineStates = new Dictionary<int, LineRuntimeState>();

        foreach (var line in configuredLines)
        {
            var state = new LineRuntimeState(
                lineId: line.LineId,
                lineNumber: line.LineNumber,
                lineName: line.LineName,
                plcIp: line.PlcIp,
                manufacturer: line.Manufacturer,
                pollIntervalMs: line.PollIntervalMs,
                metadata: BuildRunMetadata(line, line.ProductId, now));

            if (persistedRuntimeStates.TryGetValue(line.LineId, out var checkpoint))
            {
                state.ApplyPersistedCheckpoint(checkpoint);
            }

            lineStates[line.LineId] = state;
        }

        return lineStates;
    }

    // -------------------------------------------------------------------------
    // Fallback and decode helpers
    // -------------------------------------------------------------------------

    private static string NormalizeMetadataValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    }

    private static string NormalizeProductId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "N/A" : value.Trim();
    }

    private static ProductionRunMetadata BuildRunMetadata(
        Models.Plc.LineProtocolAssignmentEntity assignment,
        string productId,
        DateTime startTimeUtc)
    {
        return new ProductionRunMetadata(
            LineId: assignment.LineId,
            ProductId: NormalizeProductId(productId),
            RecipeId: NormalizeMetadataValue(assignment.RecipeId),
            MachineId: NormalizeMetadataValue(assignment.MachineId),
            OperatorName: NormalizeMetadataValue(assignment.OperatorName),
            StartTimeUtc: startTimeUtc);
    }

    private void TickStateSimulated(LineRuntimeState state, DateTime now)
    {
        var elapsedSeconds = Math.Max(0.001, (now - state.LastTickUtc).TotalSeconds);
        state.LastTickUtc = now;

        var phase = (now - state.Metadata.StartTimeUtc).TotalSeconds / 30d + state.LineId;

        var mode = Math.Sin(phase * 0.31) > -0.2 ? ControlMode.Auto : ControlMode.Manual;
        state.LastKnownControlMode = mode;
        var isStopped = Math.Sin(phase * 0.17) < -0.92;
        UpdateStatus(state, isStopped ? "Stopped" : "Running", now);

        if (isStopped)
        {
            return;
        }

        EnsureRunIsActiveForSampling(state, now);

        state.ProductionLength += elapsedSeconds * (7.5 + state.LineId * 0.4);

        var samples = new Dictionary<MeasurementZone, ZoneSample>
        {
            [MeasurementZone.BareOd] = BuildZoneSampleSimulated(state, MeasurementZone.BareOd, phase),
            [MeasurementZone.HotOd] = BuildZoneSampleSimulated(state, MeasurementZone.HotOd, phase),
            [MeasurementZone.ColdOd] = BuildZoneSampleSimulated(state, MeasurementZone.ColdOd, phase),
        };

        state.Engine.ApplySample(new ProductionTelemetrySample(
            TimestampUtc: now,
            Mode: mode,
            ProductionLength: state.ProductionLength,
            Zones: samples));
    }

    // Offline is an explicit runtime state so the UI can distinguish between a
    // stopped machine and a disconnected one.
    private async Task MarkLineOfflineAsync(LineRuntimeState state, DateTime now, CancellationToken cancellationToken)
    {
        string previousStatus;
        string currentStatus;

        lock (_gate)
        {
            previousStatus = state.Status;
            UpdateStatus(state, "Offline", now);
            state.LastTickUtc = now;
            currentStatus = state.Status;
        }

        if (!string.Equals(previousStatus, currentStatus, StringComparison.OrdinalIgnoreCase))
        {
            await PersistRuntimeEventAsync(state, "StatusSwitch", previousStatus, currentStatus, now, cancellationToken);
        }
    }

    private static ZoneSample BuildZoneSampleSimulated(LineRuntimeState state, MeasurementZone zone, double phase)
    {
        var setpoint = GetSetpoint(state.CurrentProductId, zone);
        var modulation = zone switch
        {
            MeasurementZone.BareOd => Math.Sin(phase * 0.45 + 0.1),
            MeasurementZone.HotOd => Math.Cos(phase * 0.39 + 0.4),
            MeasurementZone.ColdOd => Math.Sin(phase * 0.35 + 0.8),
            _ => 0d,
        };

        var percentDrift = 0.45 * modulation;
        var actual = setpoint * (1d + percentDrift / 100d);

        return new ZoneSample(setpoint, actual);
    }

    private static bool TryGetZoneSample(
        IReadOnlyDictionary<string, LineTagCatalogEntryDto> catalogMap,
        IReadOnlyDictionary<string, PlcTagReadResultDto> readMap,
        string setpointKey,
        string actualKey,
        out ZoneSample sample)
    {
        sample = default!;

        if (!TryGetScaledNumberByLogicalKey(catalogMap, readMap, setpointKey, out var setpoint)
            || !TryGetScaledNumberByLogicalKey(catalogMap, readMap, actualKey, out var actual))
        {
            return false;
        }

        sample = new ZoneSample(setpoint, actual);
        return true;
    }

    private static bool TryGetScaledNumberByLogicalKey(
        IReadOnlyDictionary<string, LineTagCatalogEntryDto> catalogMap,
        IReadOnlyDictionary<string, PlcTagReadResultDto> readMap,
        string logicalKey,
        out double value)
    {
        value = 0d;
        if (!catalogMap.TryGetValue(logicalKey, out var mapping)
            || string.IsNullOrWhiteSpace(mapping.PlcAddress)
            || !readMap.TryGetValue(mapping.PlcAddress.Trim(), out var read)
            || string.IsNullOrWhiteSpace(read.Value))
        {
            return false;
        }

        if (!double.TryParse(read.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var raw))
        {
            return false;
        }

        value = raw * (double)mapping.Scale;
        return true;
    }

    private static bool TryGetTextByLogicalKey(
        IReadOnlyDictionary<string, LineTagCatalogEntryDto> catalogMap,
        IReadOnlyDictionary<string, PlcTagReadResultDto> readMap,
        string logicalKey,
        out string value)
    {
        value = string.Empty;
        if (!catalogMap.TryGetValue(logicalKey, out var mapping)
            || string.IsNullOrWhiteSpace(mapping.PlcAddress)
            || !readMap.TryGetValue(mapping.PlcAddress.Trim(), out var read)
            || string.IsNullOrWhiteSpace(read.Value))
        {
            return false;
        }

        value = read.Value;
        return true;
    }

    private static bool TryGetReadableProductId(
        IReadOnlyDictionary<string, LineTagCatalogEntryDto> catalogMap,
        IReadOnlyDictionary<string, PlcTagReadResultDto> readMap,
        out string value)
    {
        value = "N/A";

        if (!TryGetTextByLogicalKey(catalogMap, readMap, LogicalKeyProductId, out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (LooksLikeUnreadableMetadataPayload(trimmed))
        {
            return true;
        }

        value = trimmed;
        return true;
    }

    private static bool LooksLikeUnreadableMetadataPayload(string raw)
    {
        if (!raw.StartsWith("{", StringComparison.Ordinal) || !raw.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        return raw.Contains("\"kind\":\"metadata\"", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("\"note\"", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("not directly readable as a scalar value", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProductIdForDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "N/A";
        }

        var trimmed = value.Trim();
        return LooksLikeUnreadableMetadataPayload(trimmed) ? "N/A" : trimmed;
    }

    private static bool TryGetControlMode(
        IReadOnlyDictionary<string, LineTagCatalogEntryDto> catalogMap,
        IReadOnlyDictionary<string, PlcTagReadResultDto> readMap,
        out ControlMode mode)
    {
        mode = ControlMode.Auto;
        if (!TryGetTextByLogicalKey(catalogMap, readMap, LogicalKeyControlMode, out var rawMode))
        {
            return false;
        }

        var normalized = rawMode.Trim().ToLowerInvariant();
        if (normalized is "1" or "auto")
        {
            mode = ControlMode.Auto;
            return true;
        }

        if (normalized is "0" or "manual")
        {
            mode = ControlMode.Manual;
            return true;
        }

        mode = normalized.Contains("manual", StringComparison.OrdinalIgnoreCase)
            ? ControlMode.Manual
            : ControlMode.Auto;
        return true;
    }

    private static bool TryResolveMachineState(
        IReadOnlyDictionary<string, LineTagCatalogEntryDto> catalogMap,
        IReadOnlyDictionary<string, PlcTagReadResultDto> readMap,
        out string state)
    {
        // Numeric PLC states are intentionally mapped explicitly so the
        // dashboard and reports keep a stable vocabulary.
        state = "Running";
        if (!TryGetTextByLogicalKey(catalogMap, readMap, LogicalKeyMachineState, out var rawState))
        {
            return false;
        }

        var normalized = rawState.Trim().ToLowerInvariant();
        // PLC state conventions: 0=Stopped, 1=Running, 2=Bleedout, 3=Startup, 4=Faulted, 5=Maintenance.
        if (normalized == "0" || normalized.Contains("stop", StringComparison.OrdinalIgnoreCase))
        {
            state = "Stopped";
            return true;
        }

        if (normalized == "1" || normalized.Contains("run", StringComparison.OrdinalIgnoreCase))
        {
            state = "Running";
            return true;
        }

        if (normalized == "2" || normalized.Contains("bleedout", StringComparison.OrdinalIgnoreCase))
        {
            state = "Bleedout";
            return true;
        }

        if (normalized == "3" || normalized.Contains("startup", StringComparison.OrdinalIgnoreCase))
        {
            state = "Startup";
            return true;
        }

        if (normalized == "4" || normalized.Contains("fault", StringComparison.OrdinalIgnoreCase))
        {
            state = "Faulted";
            return true;
        }

        if (normalized == "5" || normalized.Contains("maintenance", StringComparison.OrdinalIgnoreCase))
        {
            state = "Maintenance";
            return true;
        }

        state = "Running";
        return true;
    }

    private static double GetSetpoint(string productId, MeasurementZone zone)
    {
        return productId switch
        {
            "1/0" when zone == MeasurementZone.BareOd => 347.4,
            "1/0" when zone == MeasurementZone.HotOd => 461.4,
            "1/0" when zone == MeasurementZone.ColdOd => 459.1,
            "250 MCM" when zone == MeasurementZone.BareOd => 539.0,
            "250 MCM" when zone == MeasurementZone.HotOd => 675.0,
            "250 MCM" when zone == MeasurementZone.ColdOd => 671.6,
            _ => 100,
        };
    }

    private static DashboardLineDto ToDashboardLine(LineRuntimeState state)
    {
        var snapshot = GetSnapshotOrFallback(state);
        var runtimeSeconds = Math.Max(0L, (long)(snapshot.LastUpdateUtc - snapshot.Metadata.StartTimeUtc).TotalSeconds);
        var autoModeVariance = CalculateWeightedVariance(snapshot.AutoQuality.Zones.Values);
        var manualModeVariance = CalculateWeightedVariance(snapshot.ManualQuality.Zones.Values);
        var totalVariance = CalculateWeightedVariance(snapshot.OverallZones.Values);

        return new DashboardLineDto
        {
            Id = state.LineId,
            LineNumber = state.LineNumber,
            LineName = state.LineName,
            ProductId = NormalizeProductIdForDisplay(state.CurrentProductId),
            StartTimeUtc = state.Metadata.StartTimeUtc,
            Status = state.Status,
            ControlMode = snapshot.CurrentMode.ToString(),
            TotalLength = snapshot.CurrentProductionLength,
            RuntimeSeconds = runtimeSeconds,
            PlcIp = state.PlcIp,
            Manufacturer = state.Manufacturer,
            UpdatedAtUtc = snapshot.LastUpdateUtc,
            PercentAutoMode = snapshot.AutoPercentage,
            PercentManualMode = snapshot.ManualPercentage,
            AutoModeVariance = autoModeVariance,
            ManualModeVariance = manualModeVariance,
            TotalVariance = totalVariance,
        };
    }

    private static ProductionStatisticsSnapshot GetSnapshotOrFallback(LineRuntimeState state)
    {
        if (state.Engine.IsActive)
        {
            return state.Engine.GetSnapshot();
        }

        var emptyStats = Enum
            .GetValues<MeasurementZone>()
            .ToDictionary(zone => zone, _ => new ZoneStatisticsSnapshot());

        var emptyLive = Enum
            .GetValues<MeasurementZone>()
            .ToDictionary(zone => zone, _ => new ZoneLiveSnapshot());

        var lastUpdateUtc = state.LastTickUtc == default
            ? state.Metadata.StartTimeUtc
            : state.LastTickUtc;

        return new ProductionStatisticsSnapshot
        {
            Metadata = state.Metadata,
            LastUpdateUtc = lastUpdateUtc,
            CurrentMode = state.LastKnownControlMode,
            CurrentProductionLength = state.ProductionLength,
            AutoTimeSeconds = 0d,
            ManualTimeSeconds = 0d,
            TotalControlTimeSeconds = 0d,
            AutoPercentage = 0d,
            ManualPercentage = 0d,
            LiveZones = emptyLive,
            OverallZones = emptyStats,
            AutoQuality = new ModeQualitySnapshot
            {
                Zones = emptyStats,
            },
            ManualQuality = new ModeQualitySnapshot
            {
                Zones = emptyStats,
            },
        };
    }

    private static double CalculateWeightedVariance(IEnumerable<ZoneStatisticsSnapshot> zones)
    {
        var totalCount = 0L;
        var weightedSum = 0d;

        foreach (var zone in zones)
        {
            if (zone.MeasurementCount <= 0)
            {
                continue;
            }

            totalCount += zone.MeasurementCount;
            weightedSum += zone.AverageAbsoluteDeviation * zone.MeasurementCount;
        }

        return totalCount == 0 ? 0d : weightedSum / totalCount;
    }

    // -------------------------------------------------------------------------
    // Run lifecycle and persistence
    // -------------------------------------------------------------------------

    private async Task HandleRunLifecycleTransitionAsync(LineRuntimeState state, DateTime now, CancellationToken cancellationToken)
    {
        var stopped = IsStopState(state.Status);
        if (!stopped)
        {
            var shouldMarkRunning = false;

            lock (_gate)
            {
                if (!state.Engine.IsActive)
                {
                    var nextMetadata = state.Metadata with
                    {
                        ProductId = NormalizeProductId(state.CurrentProductId),
                        StartTimeUtc = now,
                    };

                    state.Metadata = nextMetadata;
                    state.ProductionLength = 0d;
                    state.HasSeenRunningState = false;
                    state.Engine.StartRun(nextMetadata);
                }

                shouldMarkRunning = state.Status.Equals("Running", StringComparison.OrdinalIgnoreCase);
                if (shouldMarkRunning)
                {
                    state.HasSeenRunningState = true;
                }

                state.HasPersistedCurrentStop = false;
            }

            return;
        }

        var shouldPersistStop = false;
        var shouldPersistCompletedRun = false;
        CompletedProductionRecord? completed = null;

        lock (_gate)
        {
            if (state.HasPersistedCurrentStop || !state.Engine.IsActive)
            {
                return;
            }

            shouldPersistStop = true;
            shouldPersistCompletedRun = state.HasSeenRunningState;

            if (shouldPersistCompletedRun)
            {
                completed = state.Engine.CompleteRun(now);
            }
            else
            {
                state.Engine.CompleteRun(now);
            }

            state.HasPersistedCurrentStop = true;
        }

        if (!shouldPersistStop || !shouldPersistCompletedRun || completed is null)
        {
            return;
        }

        await PersistCompletedRunAsync(state, completed, cancellationToken);
    }

    // Completed runs are persisted once per stop/fault/offline transition so the
    // reports page can reconstruct historical production activity.
    private async Task PersistActiveRuntimeStateAsync(
        LineRuntimeState state,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ActiveLineRuntimeStateEntity checkpoint;

        lock (_gate)
        {
            if (!state.ShouldPersistCheckpoint(now, ActiveRuntimePersistenceInterval))
            {
                return;
            }

            checkpoint = new ActiveLineRuntimeStateEntity
            {
                LineId = state.LineId,
                Status = state.Status,
                ControlMode = state.LastKnownControlMode.ToString(),
                CurrentProductId = NormalizeProductId(state.CurrentProductId),
                RunStartTimeUtc = state.Metadata.StartTimeUtc,
                LastTickUtc = state.LastTickUtc,
                ProductionLength = state.ProductionLength,
                HasSeenRunningState = state.HasSeenRunningState,
                HasPersistedCurrentStop = state.HasPersistedCurrentStop,
                UpdatedAtUtc = now,
            };

            state.MarkCheckpointPersisted(now);
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();

            var existing = await dbContext.ActiveLineRuntimeStates
                .FirstOrDefaultAsync(x => x.LineId == checkpoint.LineId, cancellationToken);

            if (existing is null)
            {
                dbContext.ActiveLineRuntimeStates.Add(checkpoint);
            }
            else
            {
                existing.Status = checkpoint.Status;
                existing.ControlMode = checkpoint.ControlMode;
                existing.CurrentProductId = checkpoint.CurrentProductId;
                existing.RunStartTimeUtc = checkpoint.RunStartTimeUtc;
                existing.LastTickUtc = checkpoint.LastTickUtc;
                existing.ProductionLength = checkpoint.ProductionLength;
                existing.HasSeenRunningState = checkpoint.HasSeenRunningState;
                existing.HasPersistedCurrentStop = checkpoint.HasPersistedCurrentStop;
                existing.UpdatedAtUtc = checkpoint.UpdatedAtUtc;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to persist active runtime checkpoint for line {LineId}.", state.LineId);
        }
    }

    private async Task PersistCompletedRunAsync(
        LineRuntimeState state,
        CompletedProductionRecord completed,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = completed.Snapshot;
            var runtimeSeconds = Math.Max(0d, (completed.EndTimeUtc - snapshot.Metadata.StartTimeUtc).TotalSeconds);

            var run = new CompletedProductionRunEntity
            {
                Id = Guid.NewGuid(),
                LineId = state.LineId,
                LineNumber = state.LineNumber,
                LineName = state.LineName,
                ProductId = NormalizeProductId(state.CurrentProductId),
                RecipeId = snapshot.Metadata.RecipeId,
                MachineId = snapshot.Metadata.MachineId,
                OperatorName = snapshot.Metadata.OperatorName,
                Manufacturer = state.Manufacturer,
                PlcIp = state.PlcIp,
                FinalStatus = state.Status,
                StartTimeUtc = snapshot.Metadata.StartTimeUtc,
                EndTimeUtc = completed.EndTimeUtc,
                RuntimeSeconds = runtimeSeconds,
                ProductionLength = snapshot.CurrentProductionLength,
                AutoTimeSeconds = snapshot.AutoTimeSeconds,
                ManualTimeSeconds = snapshot.ManualTimeSeconds,
                AutoPercentage = snapshot.AutoPercentage,
                ManualPercentage = snapshot.ManualPercentage,
                CreatedAtUtc = DateTime.UtcNow,
                ZoneStats = BuildZoneStats(snapshot),
            };

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();
            dbContext.CompletedProductionRuns.Add(run);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to persist completed run for line {LineId}.", state.LineId);
        }
    }

    // Runtime events capture state changes that matter to operators and reports.
    private async Task PersistRuntimeEventAsync(
        LineRuntimeState state,
        string eventType,
        string previousValue,
        string currentValue,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (string.Equals(previousValue, currentValue, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();

            dbContext.RuntimeEvents.Add(new RuntimeEventEntity
            {
                Id = Guid.NewGuid(),
                LineId = state.LineId,
                LineNumber = state.LineNumber,
                LineName = state.LineName,
                EventType = eventType,
                PreviousValue = previousValue,
                CurrentValue = currentValue,
                OccurredAtUtc = occurredAtUtc,
                CreatedAtUtc = DateTime.UtcNow,
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to persist runtime event for line {LineId}. Type={EventType} {Previous}->{Current}",
                state.LineId,
                eventType,
                previousValue,
                currentValue);
        }
    }

    // -------------------------------------------------------------------------
    // Small runtime helpers
    // -------------------------------------------------------------------------

    private static List<CompletedProductionRunZoneStatEntity> BuildZoneStats(ProductionStatisticsSnapshot snapshot)
    {
        var rows = new List<CompletedProductionRunZoneStatEntity>();
        foreach (var zone in Enum.GetValues<MeasurementZone>())
        {
            rows.Add(CreateZoneStat(zone, "overall", snapshot.OverallZones[zone]));
            rows.Add(CreateZoneStat(zone, "auto", snapshot.AutoQuality.Zones[zone]));
            rows.Add(CreateZoneStat(zone, "manual", snapshot.ManualQuality.Zones[zone]));
        }

        return rows;
    }

    private static CompletedProductionRunZoneStatEntity CreateZoneStat(
        MeasurementZone zone,
        string segment,
        ZoneStatisticsSnapshot stats)
    {
        return new CompletedProductionRunZoneStatEntity
        {
            Zone = zone.ToString(),
            Segment = segment,
            MeasurementCount = stats.MeasurementCount,
            SkippedCount = stats.SkippedCount,
            RunningAbsoluteDeviationSum = stats.RunningAbsoluteDeviationSum,
            AverageAbsoluteDeviation = stats.AverageAbsoluteDeviation,
            MaxPositiveDeviation = stats.MaxPositiveDeviation,
            MaxNegativeDeviation = stats.MaxNegativeDeviation,
            CurrentDeviation = stats.CurrentDeviation,
        };
    }

    private static bool IsStopState(string status)
    {
        return status.Equals("Stopped", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Offline", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Faulted", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Completed", StringComparison.OrdinalIgnoreCase);
    }

    private static void UpdateStatus(LineRuntimeState state, string nextStatus, DateTime now)
    {
        if (!string.Equals(state.Status, nextStatus, StringComparison.OrdinalIgnoreCase))
        {
            state.StatusEnteredUtc = now;
        }

        state.Status = nextStatus;
    }

    private static string ZoneToLabel(MeasurementZone zone)
    {
        return zone switch
        {
            MeasurementZone.BareOd => "Bare",
            MeasurementZone.HotOd => "Hot",
            MeasurementZone.ColdOd => "Cold",
            _ => zone.ToString(),
        };
    }

    // Mutable in-memory state for a single configured line.
    private sealed class LineRuntimeState
    {
        public int LineId { get; }
        private int _tickInProgress;

        public int LineNumber { get; set; }
        public string LineName { get; set; }
        public string PlcIp { get; set; }
        public string Manufacturer { get; set; }
        public int PollIntervalMs { get; set; }
        public DateTime NextPollAtUtc { get; private set; }
        public bool IsEnabled { get; set; } = true;
        public string Status { get; set; } = "Offline";
        public ProductionRunMetadata Metadata { get; set; }
        public string CurrentProductId { get; set; }
        public ProductionStatisticsEngine Engine { get; }
        public DateTime LastTickUtc { get; set; }
        public DateTime StatusEnteredUtc { get; set; }
        public double ProductionLength { get; set; }
        public ControlMode LastKnownControlMode { get; set; } = ControlMode.Manual;
        public bool HasPersistedCurrentStop { get; set; }
        public bool HasSeenRunningState { get; set; }
        public DateTime LastCheckpointPersistedUtc { get; private set; }

        public LineRuntimeState(
            int lineId,
            int lineNumber,
            string lineName,
            string plcIp,
            string manufacturer,
            int pollIntervalMs,
            ProductionRunMetadata metadata)
        {
            LineId = lineId;
            LineNumber = lineNumber;
            LineName = lineName;
            PlcIp = plcIp;
            Manufacturer = manufacturer;
            PollIntervalMs = Math.Clamp(pollIntervalMs, 500, 60000);
            Metadata = metadata;
            CurrentProductId = metadata.ProductId;

            Engine = new ProductionStatisticsEngine();
            Engine.StartRun(metadata);
            LastTickUtc = metadata.StartTimeUtc;
            StatusEnteredUtc = metadata.StartTimeUtc;
            NextPollAtUtc = metadata.StartTimeUtc;
            LastCheckpointPersistedUtc = DateTime.MinValue;
        }

        public void ApplyPersistedCheckpoint(ActiveLineRuntimeStateEntity checkpoint)
        {
            CurrentProductId = string.IsNullOrWhiteSpace(checkpoint.CurrentProductId)
                ? CurrentProductId
                : checkpoint.CurrentProductId.Trim();

            Status = string.IsNullOrWhiteSpace(checkpoint.Status)
                ? Status
                : checkpoint.Status.Trim();

            if (Enum.TryParse<ControlMode>(checkpoint.ControlMode, ignoreCase: true, out var persistedMode))
            {
                LastKnownControlMode = persistedMode;
            }

            var persistedStart = checkpoint.RunStartTimeUtc == default
                ? Metadata.StartTimeUtc
                : checkpoint.RunStartTimeUtc;

            Metadata = Metadata with
            {
                ProductId = CurrentProductId,
                StartTimeUtc = persistedStart,
            };

            LastTickUtc = checkpoint.LastTickUtc == default ? LastTickUtc : checkpoint.LastTickUtc;
            StatusEnteredUtc = LastTickUtc;
            ProductionLength = checkpoint.ProductionLength;
            HasSeenRunningState = checkpoint.HasSeenRunningState;
            HasPersistedCurrentStop = checkpoint.HasPersistedCurrentStop;
        }

        public bool ShouldPersistCheckpoint(DateTime now, TimeSpan interval)
        {
            return LastCheckpointPersistedUtc == DateTime.MinValue
                || now - LastCheckpointPersistedUtc >= interval;
        }

        public void MarkCheckpointPersisted(DateTime now)
        {
            LastCheckpointPersistedUtc = now;
        }

        public bool TryBeginTick()
        {
            return Interlocked.CompareExchange(ref _tickInProgress, 1, 0) == 0;
        }

        public void CompleteTick(DateTime now)
        {
            NextPollAtUtc = now.AddMilliseconds(Math.Clamp(PollIntervalMs, 500, 60000));
            Interlocked.Exchange(ref _tickInProgress, 0);
        }
    }
}
