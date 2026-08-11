using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace backend.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _databasePath;
    private readonly string _locksDirectory;
    private readonly bool _cleanOnStart;
    private readonly bool _cleanOnDispose;
    private readonly string? _previousDatabasePathEnv;
    private readonly string? _previousConnectionStringEnv;
    private readonly string? _previousDisableRateLimiterEnv;

    public TestWebApplicationFactory()
        : this(databasePath: null, cleanOnStart: true, cleanOnDispose: true)
    {
    }

    internal TestWebApplicationFactory(
        string? databasePath = null,
        bool cleanOnStart = true,
        bool cleanOnDispose = true)
    {
        _databasePath = databasePath
            ?? Path.Combine(Path.GetTempPath(), $"plantmonitor-tests-{Guid.NewGuid():N}.db");
        _locksDirectory = Path.Combine(Path.GetDirectoryName(_databasePath) ?? AppContext.BaseDirectory, ".locks");
        _cleanOnStart = cleanOnStart;
        _cleanOnDispose = cleanOnDispose;

        _previousDatabasePathEnv = Environment.GetEnvironmentVariable("App__DatabasePath");
        _previousConnectionStringEnv = Environment.GetEnvironmentVariable("ConnectionStrings__PlantMonitor");
        _previousDisableRateLimiterEnv = Environment.GetEnvironmentVariable("App__DisableLoginRateLimiter");

        Environment.SetEnvironmentVariable("App__DatabasePath", _databasePath);
        Environment.SetEnvironmentVariable("ConnectionStrings__PlantMonitor", $"Data Source={_databasePath}");
        Environment.SetEnvironmentVariable("App__DisableLoginRateLimiter", "true");

        if (_cleanOnStart)
        {
            CleanupDatabaseArtifacts();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["App:DisableLoginRateLimiter"] = "true",
                ["App:DatabasePath"] = _databasePath,
                ["ConnectionStrings:PlantMonitor"] = $"Data Source={_databasePath}",
            };

            configBuilder.AddInMemoryCollection(overrides);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        Environment.SetEnvironmentVariable("App__DatabasePath", _previousDatabasePathEnv);
        Environment.SetEnvironmentVariable("ConnectionStrings__PlantMonitor", _previousConnectionStringEnv);
        Environment.SetEnvironmentVariable("App__DisableLoginRateLimiter", _previousDisableRateLimiterEnv);

        if (_cleanOnDispose)
        {
            CleanupDatabaseArtifacts();
        }
    }

    private void CleanupDatabaseArtifacts()
    {
        TryDelete(_databasePath);
        TryDelete(_databasePath + "-wal");
        TryDelete(_databasePath + "-shm");
        TryDeleteDirectory(_locksDirectory);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}