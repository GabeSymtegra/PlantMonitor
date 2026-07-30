using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace backend.Tests;

public class AuthAndAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthAndAuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidAdminCredentials_ReturnsRoleAndCookie()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "test",
            password = "test",
            rememberMe = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("Admin", payload?["role"]?.GetValue<string>());
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, value => value.Contains("pm_auth=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "test",
            password = "wrong",
            rememberMe = true,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithMissingCredentials_ReturnsProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "",
            password = "",
            rememberMe = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("Invalid request parameters.", payload!["title"]?.GetValue<string>());
        Assert.Equal(400, payload["status"]?.GetValue<int>());
        Assert.Equal("missing_credentials", payload["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task ConfigurationAccess_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/configuration/access-check");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConfigurationAccess_WithOperatorToken_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "operator", "test");
        var response = await client.GetAsync("/api/configuration/access-check");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConfigurationAccess_WithAdminToken_ReturnsOk()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");
        var response = await client.GetAsync("/api/configuration/access-check");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PlcPresetAccess_WithAdminToken_ReturnsOk()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");
        var response = await client.GetAsync("/api/admin/plc/presets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PlcPresetAccess_WithOperatorToken_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "operator", "test");
        var response = await client.GetAsync("/api/admin/plc/presets");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlcProtocolAssignment_WithAdminToken_CanUpsertAndReadBack()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");
        await EnsureLineExistsAsync(client, 101, "192.168.20.101");

        var upsertResponse = await client.PutAsJsonAsync("/api/admin/lines/101/protocol-assignment", new
        {
            manufacturer = "AllenBradley",
            presetName = "BasicStatus",
            presetVersion = 1,
            pollIntervalMs = 1500,
        });

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);

        var assignmentResponse = await client.GetAsync("/api/admin/lines/101/protocol-assignment");
        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);

        var payload = await assignmentResponse.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal(101, payload?["lineId"]?.GetValue<int>());
        Assert.Equal("AllenBradley", payload?["manufacturer"]?.GetValue<string>());
        Assert.Equal("BasicStatus", payload?["presetName"]?.GetValue<string>());
        Assert.Equal(1500, payload?["pollIntervalMs"]?.GetValue<int>());
    }

    [Fact]
    public async Task PlcReadTag_WithoutTagName_ReturnsProblemDetails()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PostAsJsonAsync("/api/admin/plc/read-tag", new
        {
            driver = "AllenBradley",
            ipAddress = "127.0.0.1",
            tagName = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("Invalid request parameters.", payload!["title"]?.GetValue<string>());
        Assert.Equal(400, payload["status"]?.GetValue<int>());
        Assert.Equal("missing_tag_name", payload["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task DeleteCompletedRun_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/api/production-runs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCompletedRun_WithViewerToken_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "viewer", "test");

        var response = await client.DeleteAsync($"/api/production-runs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCompletedRun_WithOperatorToken_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "operator", "test");

        var response = await client.DeleteAsync($"/api/production-runs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCompletedRun_WithAdminToken_IsAuthorized()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.DeleteAsync($"/api/production-runs/{Guid.NewGuid()}");
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string username, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username,
            password,
            rememberMe = true,
        });

        loginResponse.EnsureSuccessStatusCode();
    }

    private static async Task EnsureLineExistsAsync(HttpClient client, int lineId, string plcIp)
    {
        var createResponse = await client.PostAsJsonAsync("/api/lines", new
        {
            lineNumber = lineId,
            lineName = $"Line {lineId}",
            productId = "123-456-78-9",
            recipeId = "RCP-TEST",
            machineId = "MX-TEST",
            operatorName = "operator-test",
            plcIp,
            manufacturer = "AllenBradley",
            pollIntervalMs = 1500,
            isActive = false,
            lineLifecycleState = "Draft",
        });

        createResponse.EnsureSuccessStatusCode();
    }
}
