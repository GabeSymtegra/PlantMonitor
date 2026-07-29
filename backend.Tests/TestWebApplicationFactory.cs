using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace backend.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _databasePath;
    private readonly string _locksDirectory;

    public TestWebApplicationFactory()
    {
        _databasePath = Path.Combine(AppContext.BaseDirectory, "plantmonitor.db");
        _locksDirectory = Path.Combine(AppContext.BaseDirectory, ".locks");

        TryDelete(_databasePath);
        TryDelete(_databasePath + "-wal");
        TryDelete(_databasePath + "-shm");
        TryDeleteDirectory(_locksDirectory);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["App:DisableLoginRateLimiter"] = "true",
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

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch
        {
            // Best-effort cleanup for test database files.
        }
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