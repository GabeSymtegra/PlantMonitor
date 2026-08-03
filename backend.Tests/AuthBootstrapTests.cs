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
