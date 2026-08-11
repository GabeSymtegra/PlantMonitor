using System.Net;
using System.Net.Http.Json;
using backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace backend.Tests;

public sealed class AuthSecurityBehaviorTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthSecurityBehaviorTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_SetsHttpOnlyAuthCookie()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "test",
            password = "test",
            rememberMe = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, value =>
            value.Contains("pm_auth=", StringComparison.OrdinalIgnoreCase)
            && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProtectedEndpoint_WithMustChangePassword_ReturnsForbidden()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();
            var user = await dbContext.LocalUsers.SingleAsync(x => x.Username == "test");
            user.MustChangePassword = true;
            await dbContext.SaveChangesAsync();
        }

        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "test",
            password = "test",
            rememberMe = true,
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var response = await client.GetAsync("/api/production-runs");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();
            var user = await dbContext.LocalUsers.SingleAsync(x => x.Username == "test");
            user.MustChangePassword = false;
            await dbContext.SaveChangesAsync();
        }
    }
}
