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