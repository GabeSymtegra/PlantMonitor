using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace backend.Tests;

public sealed class ProductionPersistenceApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProductionPersistenceApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RecipeTolerances_WithAdminToken_ReturnsSeededRows()
    {
        var client = _factory.CreateClient();
        var token = await GetAccessToken(client, "test", "test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/recipe-tolerances");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonArray>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!);

        var first = payload![0]?.AsObject();
        Assert.NotNull(first);
        Assert.False(string.IsNullOrWhiteSpace(first!["recipeId"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(first["measurementType"]?.GetValue<string>()));
    }

    [Fact]
    public async Task ProductionRuns_WithAdminToken_ReturnsOkArray()
    {
        var client = _factory.CreateClient();
        var token = await GetAccessToken(client, "test", "test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/production-runs?take=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonArray>();
        Assert.NotNull(payload);
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
