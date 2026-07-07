using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace backend.Tests;

public class AuthAndAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthAndAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidAdminCredentials_ReturnsTokenAndRole()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "test",
            password = "test"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload?["accessToken"]?.GetValue<string>()));
        Assert.Equal("Admin", payload?["role"]?.GetValue<string>());
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "test",
            password = "wrong"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        var token = await GetAccessToken(client, "operator", "test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/configuration/access-check");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConfigurationAccess_WithAdminToken_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var token = await GetAccessToken(client, "test", "test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/configuration/access-check");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PlcPresetAccess_WithAdminToken_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var token = await GetAccessToken(client, "test", "test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/admin/plc/presets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PlcPresetAccess_WithOperatorToken_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await GetAccessToken(client, "operator", "test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/admin/plc/presets");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlcProtocolAssignment_WithAdminToken_CanUpsertAndReadBack()
    {
        var client = _factory.CreateClient();
        var token = await GetAccessToken(client, "test", "test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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

    private static async Task<string> GetAccessToken(HttpClient client, string username, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username,
            password
        });

        loginResponse.EnsureSuccessStatusCode();

        var payload = await loginResponse.Content.ReadFromJsonAsync<JsonObject>();
        var token = payload?["accessToken"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Access token was not returned by login endpoint.");
        }

        return token;
    }
}
