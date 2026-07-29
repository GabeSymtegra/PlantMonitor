using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace backend.Tests;

public sealed class AuthSecurityBehaviorTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthSecurityBehaviorTests(WebApplicationFactory<Program> factory)
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
}
