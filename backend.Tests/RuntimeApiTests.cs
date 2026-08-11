using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace backend.Tests;

public sealed class RuntimeApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RuntimeApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Dashboard_WithAdminToken_ReturnsSnapshotContract()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!["lines"]?.AsArray());
        Assert.False(string.IsNullOrWhiteSpace(payload["lastUpdatedUtc"]?.GetValue<string>()));
    }

    [Fact]
    public async Task LineDetails_WithUnknownLine_ReturnsProblemDetails()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.GetAsync("/api/lines/9999/details");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("Resource not found.", payload!["title"]?.GetValue<string>());
        Assert.Equal("line_runtime_not_found", payload["code"]?.GetValue<string>());
        Assert.Equal(404, payload["status"]?.GetValue<int>());
    }

    [Fact]
    public async Task Dashboard_LinesHaveValidRuntimeShape_WhenPresent()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.GetAsync("/api/dashboard");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        var lines = payload?["lines"]?.AsArray();
        Assert.NotNull(lines);

        foreach (var lineNode in lines!)
        {
            var line = lineNode?.AsObject();
            Assert.NotNull(line);

            Assert.True((line!["id"]?.GetValue<int>() ?? 0) > 0);
            Assert.False(string.IsNullOrWhiteSpace(line["status"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(line["controlMode"]?.GetValue<string>()));

            var autoPercentage = line["autoPercentage"]?.GetValue<double>() ?? -1;
            var manualPercentage = line["manualPercentage"]?.GetValue<double>() ?? -1;

            Assert.InRange(autoPercentage, 0, 100);
            Assert.InRange(manualPercentage, 0, 100);
            Assert.True(autoPercentage + manualPercentage <= 100.0001d);
        }
    }

    private static async Task LoginAsync(HttpClient client, string username, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username,
            password,
        });

        loginResponse.EnsureSuccessStatusCode();
    }
}