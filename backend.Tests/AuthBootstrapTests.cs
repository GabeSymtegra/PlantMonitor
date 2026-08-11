using backend.Data;
using backend.Models.Authentication;
using backend.Services.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace backend.Tests;

public class AuthBootstrapTests
{
    [Fact]
    public async Task EnsureBootstrapUsersAsync_SeedsDevelopmentAccountsInTestingEnvironment()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<PlantMonitorDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new PlantMonitorDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var environment = new FakeWebHostEnvironment();
        environment.EnvironmentName = "Testing";
        var configuration = new ConfigurationBuilder().Build();
        var passwordHasher = new PasswordHasher<LocalUserEntity>();
        var service = new LocalUserAuthService(context, environment, configuration, passwordHasher);

        await service.EnsureBootstrapUsersAsync();

        var usernames = await context.LocalUsers.Select(x => x.Username).ToListAsync();

        Assert.Contains("test", usernames);
        Assert.Contains("operator", usernames);
        Assert.Contains("viewer", usernames);
        Assert.DoesNotContain("admin", usernames);
    }

    [Fact]
    public async Task EnsureBootstrapUsersAsync_SeedsConfiguredProductionAdminAndViewerAccounts()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<PlantMonitorDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new PlantMonitorDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var environment = new FakeWebHostEnvironment();
        environment.EnvironmentName = Environments.Production;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:BootstrapAdminPassword"] = "AdminPassword123!",
                ["Auth:BootstrapViewerPassword"] = "ViewerPassword123!",
            })
            .Build();
        var passwordHasher = new PasswordHasher<LocalUserEntity>();
        var service = new LocalUserAuthService(context, environment, configuration, passwordHasher);

        await service.EnsureBootstrapUsersAsync();

        var users = await context.LocalUsers
            .AsNoTracking()
            .OrderBy(x => x.Username)
            .Select(x => new { x.Username, x.Role, x.MustChangePassword })
            .ToListAsync();

        Assert.Contains(users, x => x.Username == "admin" && x.Role == "Admin" && x.MustChangePassword);
        Assert.Contains(users, x => x.Username == "viewer" && x.Role == "Viewer" && !x.MustChangePassword);
    }

    [Fact]
    public async Task EnsureBootstrapUsersAsync_LanDeployment_UpsertsAdminOperatorViewerWithTestPassword()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<PlantMonitorDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new PlantMonitorDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var passwordHasher = new PasswordHasher<LocalUserEntity>();
        var preexisting = new LocalUserEntity
        {
            Username = "admin",
            Role = "Viewer",
            MustChangePassword = true,
            IsDisabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        preexisting.PasswordHash = passwordHasher.HashPassword(preexisting, "LegacyPassword123!");
        context.LocalUsers.Add(preexisting);
        await context.SaveChangesAsync();

        var environment = new FakeWebHostEnvironment { EnvironmentName = Environments.Production };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:IsLanDeployment"] = "true",
            })
            .Build();
        var service = new LocalUserAuthService(context, environment, configuration, passwordHasher);

        await service.EnsureBootstrapUsersAsync();

        var users = await context.LocalUsers
            .AsNoTracking()
            .OrderBy(x => x.Username)
            .Select(x => new { x.Username, x.Role, x.MustChangePassword, x.IsDisabled })
            .ToListAsync();

        Assert.Contains(users, x => x.Username == "admin" && x.Role == "Admin" && !x.MustChangePassword && !x.IsDisabled);
        Assert.Contains(users, x => x.Username == "operator" && x.Role == "Operator" && !x.MustChangePassword && !x.IsDisabled);
        Assert.Contains(users, x => x.Username == "viewer" && x.Role == "Viewer" && !x.MustChangePassword && !x.IsDisabled);

        var adminAuth = await service.AuthenticateAsync("admin", "test");
        var operatorAuth = await service.AuthenticateAsync("operator", "test");
        var viewerAuth = await service.AuthenticateAsync("viewer", "test");

        Assert.True(adminAuth.Success);
        Assert.True(operatorAuth.Success);
        Assert.True(viewerAuth.Success);
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "backend.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
