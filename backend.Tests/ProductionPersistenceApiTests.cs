using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace backend.Tests;

public sealed class ProductionPersistenceApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ProductionPersistenceApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RecipeTolerances_WithAdminToken_ReturnsSeededRows()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

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
    public async Task ProductionRuns_WithAdminToken_ReturnsPagedPayload()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.GetAsync("/api/production-runs?take=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.True(payload!.ContainsKey("items"));
        Assert.True(payload.ContainsKey("totalCount"));
    }

    [Fact]
    public async Task RuntimeEvents_WithInvalidLineId_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.GetAsync("/api/reports/events?lineId=9999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("Invalid request parameters.", payload!["title"]?.GetValue<string>());
        Assert.Equal(400, payload["status"]?.GetValue<int>());
        Assert.Equal("lineId was not found.", payload["detail"]?.GetValue<string>());
        Assert.Equal("line_not_found", payload["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task ProductionRuns_WithInvalidDateRange_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.GetAsync("/api/production-runs?fromDate=2026-07-10&toDate=2026-07-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("Invalid request parameters.", payload!["title"]?.GetValue<string>());
        Assert.Equal(400, payload["status"]?.GetValue<int>());
        Assert.Equal("toDate must be on or after fromDate.", payload["detail"]?.GetValue<string>());
        Assert.Equal("invalid_date_range", payload["code"]?.GetValue<string>());
    }

    private static async Task LoginAsync(HttpClient client, string username, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username,
            password
        });

        loginResponse.EnsureSuccessStatusCode();
    }
}
