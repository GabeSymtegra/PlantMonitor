using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json;
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

    [Fact]
    public async Task LineAndTagAssignments_PersistAcrossRestart()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"plantmonitor-restart-{Guid.NewGuid():N}.db");
        var testLineId = Random.Shared.Next(30000, 60000);
        var testPlcIp = $"10.250.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}";

        try
        {
            using (var firstHost = new TestWebApplicationFactory(tempDbPath, cleanOnStart: true, cleanOnDispose: false))
            {
                var client = firstHost.CreateClient();
                await LoginAsync(client, "test", "test");

                var createResponse = await client.PostAsJsonAsync("/api/lines", new
                {
                    lineNumber = testLineId,
                    lineName = "Restart Persistence Line",
                    productId = "123-456-78-9",
                    recipeId = "RCP-950",
                    machineId = "MX-950",
                    operatorName = "operator-950",
                    plcIp = testPlcIp,
                    manufacturer = "AllenBradley",
                    pollIntervalMs = 2000,
                    isActive = false,
                    lineLifecycleState = "Draft",
                });

                createResponse.EnsureSuccessStatusCode();
            }

            using (var secondHost = new TestWebApplicationFactory(tempDbPath, cleanOnStart: false, cleanOnDispose: true))
            {
                var restartedClient = secondHost.CreateClient();
                await LoginAsync(restartedClient, "test", "test");

                var linesResponse = await restartedClient.GetAsync("/api/lines");
                linesResponse.EnsureSuccessStatusCode();

                var linesPayload = await linesResponse.Content.ReadFromJsonAsync<JsonArray>();
                Assert.NotNull(linesPayload);
                Assert.Contains(linesPayload!, line => line?["lineNumber"]?.GetValue<int>() == testLineId);

                var assignmentResponse = await restartedClient.GetAsync($"/api/admin/lines/{testLineId}/protocol-assignment");
                assignmentResponse.EnsureSuccessStatusCode();

                var assignmentPayload = await assignmentResponse.Content.ReadFromJsonAsync<JsonObject>();
                Assert.NotNull(assignmentPayload);
                Assert.Equal(testLineId, assignmentPayload!["lineId"]?.GetValue<int>());
                Assert.Equal("AllenBradley", assignmentPayload["manufacturer"]?.GetValue<string>());
            }
        }
        finally
        {
            TryDelete(tempDbPath);
            TryDelete(tempDbPath + "-wal");
            TryDelete(tempDbPath + "-shm");
        }
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
            // Best-effort cleanup for temporary persistence test artifacts.
        }
    }
}
