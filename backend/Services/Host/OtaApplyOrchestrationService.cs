using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using backend.Data;
using backend.DTOs.System;
using backend.Interfaces;

namespace backend.Services.Host;

public sealed class OtaApplyOrchestrationService : IOtaApplyOrchestrationService
{
    private readonly IOtaPackageStagingService _stagingService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, OtaApplyOperationStatusDto> _operations =
        new(StringComparer.OrdinalIgnoreCase);

    public OtaApplyOrchestrationService(
        IOtaPackageStagingService stagingService,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration)
    {
        _stagingService = stagingService;
        _serviceScopeFactory = serviceScopeFactory;
        _configuration = configuration;
    }

    public async Task<OtaApplyOperationStatusDto> ApplyAsync(
        OtaApplyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var stage = _stagingService.GetStatus(request.StageOperationId);
        if (stage is null)
        {
            return Failed("staging_operation_not_found", "Staging operation was not found.", "unknown", "unknown");
        }

        if (!string.Equals(stage.Status, "staged", StringComparison.OrdinalIgnoreCase))
        {
            return Failed("staging_not_ready", "Package staging is not in staged state.", stage.TargetVersion, ResolveCurrentVersion());
        }

        if (string.IsNullOrWhiteSpace(stage.PackagePath) || !File.Exists(stage.PackagePath))
        {
            return Failed("staged_package_missing", "Staged package file was not found.", stage.TargetVersion, ResolveCurrentVersion());
        }

        var state = LoadState();
        var previousVersion = string.IsNullOrWhiteSpace(state.CurrentVersion)
            ? ResolveCurrentVersion()
            : state.CurrentVersion;

        var operation = new OtaApplyOperationStatusDto
        {
            OperationId = Guid.NewGuid().ToString("N"),
            Status = "applying",
            TargetVersion = stage.TargetVersion,
            PreviousVersion = previousVersion,
            CurrentVersion = stage.TargetVersion,
            AppliedPackagePath = stage.PackagePath,
            StartedAtUtc = DateTime.UtcNow,
            Message = "OTA apply orchestration started.",
        };

        _operations[operation.OperationId] = operation;

        try
        {
            var applyRoot = ResolveApplyRoot();
            var versionSegment = SanitizeSegment(stage.TargetVersion);
            var applyDirectory = Path.Combine(applyRoot, versionSegment);
            Directory.CreateDirectory(applyDirectory);

            var packageFileName = Path.GetFileName(stage.PackagePath);
            var destinationPath = Path.Combine(applyDirectory, packageFileName);
            File.Copy(stage.PackagePath, destinationPath, overwrite: true);

            state.PreviousVersion = previousVersion;
            state.CurrentVersion = stage.TargetVersion;
            state.LastAppliedPackagePath = destinationPath;
            state.LastUpdatedUtc = DateTime.UtcNow;
            SaveState(state);

            var healthPassed = await RunHealthCheckAsync(request.ForceHealthFailure, cancellationToken);
            operation.HealthCheckStatus = healthPassed ? "passed" : "failed";

            if (!healthPassed)
            {
                state.CurrentVersion = previousVersion;
                state.LastUpdatedUtc = DateTime.UtcNow;
                SaveState(state);

                operation.Status = "rolled_back";
                operation.RolledBack = true;
                operation.CurrentVersion = previousVersion;
                operation.Message = "Health check failed after apply. Automatic rollback completed.";
                operation.CompletedAtUtc = DateTime.UtcNow;
                return operation;
            }

            operation.Status = "applied";
            operation.RolledBack = false;
            operation.CurrentVersion = stage.TargetVersion;
            operation.Message = "OTA apply completed and health check passed.";
            operation.CompletedAtUtc = DateTime.UtcNow;
            return operation;
        }
        catch (Exception ex)
        {
            state.CurrentVersion = previousVersion;
            state.LastUpdatedUtc = DateTime.UtcNow;
            SaveState(state);

            operation.Status = "rolled_back";
            operation.RolledBack = true;
            operation.CurrentVersion = previousVersion;
            operation.HealthCheckStatus = "failed";
            operation.Message = $"OTA apply failed and rollback was triggered: {ex.Message}";
            operation.CompletedAtUtc = DateTime.UtcNow;
            return operation;
        }
    }

    public OtaApplyOperationStatusDto? GetStatus(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return null;
        }

        return _operations.TryGetValue(operationId.Trim(), out var value)
            ? value
            : null;
    }

    private async Task<bool> RunHealthCheckAsync(bool forceFailure, CancellationToken cancellationToken)
    {
        if (forceFailure)
        {
            return false;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();
        return await dbContext.Database.CanConnectAsync(cancellationToken);
    }

    private OtaApplyState LoadState()
    {
        var path = ResolveStateFilePath();
        if (!File.Exists(path))
        {
            return new OtaApplyState
            {
                CurrentVersion = ResolveCurrentVersion(),
                PreviousVersion = ResolveCurrentVersion(),
                LastAppliedPackagePath = null,
                LastUpdatedUtc = DateTime.UtcNow,
            };
        }

        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<OtaApplyState>(json);
            if (parsed is null)
            {
                throw new InvalidOperationException("State payload is empty.");
            }

            if (string.IsNullOrWhiteSpace(parsed.CurrentVersion))
            {
                parsed.CurrentVersion = ResolveCurrentVersion();
            }

            if (string.IsNullOrWhiteSpace(parsed.PreviousVersion))
            {
                parsed.PreviousVersion = parsed.CurrentVersion;
            }

            return parsed;
        }
        catch
        {
            return new OtaApplyState
            {
                CurrentVersion = ResolveCurrentVersion(),
                PreviousVersion = ResolveCurrentVersion(),
                LastAppliedPackagePath = null,
                LastUpdatedUtc = DateTime.UtcNow,
            };
        }
    }

    private void SaveState(OtaApplyState state)
    {
        var path = ResolveStateFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        File.WriteAllText(path, json);
    }

    private string ResolveStateFilePath()
    {
        var configured = _configuration["App:OtaStatePath"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PlantMonitor", "OtaState");
        return Path.Combine(root, "state.json");
    }

    private string ResolveApplyRoot()
    {
        var configured = _configuration["App:OtaApplyRoot"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PlantMonitor", "OtaApplied");
    }

    private static string ResolveCurrentVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var informational = entryAssembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return NormalizeVersion(informational);
        }

        var assemblyVersion = entryAssembly?.GetName().Version?.ToString();
        return string.IsNullOrWhiteSpace(assemblyVersion)
            ? "unknown"
            : NormalizeVersion(assemblyVersion);
    }

    private static string NormalizeVersion(string value)
    {
        var trimmed = value.Split('+', 2, StringSplitOptions.TrimEntries)[0].Trim();
        return trimmed.StartsWith('v') || trimmed.StartsWith('V')
            ? trimmed[1..]
            : trimmed;
    }

    private static string SanitizeSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown-version" : sanitized;
    }

    private static OtaApplyOperationStatusDto Failed(string code, string message, string targetVersion, string currentVersion)
    {
        return new OtaApplyOperationStatusDto
        {
            OperationId = Guid.NewGuid().ToString("N"),
            Status = code,
            TargetVersion = targetVersion,
            PreviousVersion = currentVersion,
            CurrentVersion = currentVersion,
            HealthCheckStatus = "not_run",
            RolledBack = false,
            Message = message,
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
        };
    }

    private sealed class OtaApplyState
    {
        public required string CurrentVersion { get; set; }
        public required string PreviousVersion { get; set; }
        public string? LastAppliedPackagePath { get; set; }
        public required DateTime LastUpdatedUtc { get; set; }
    }
}
