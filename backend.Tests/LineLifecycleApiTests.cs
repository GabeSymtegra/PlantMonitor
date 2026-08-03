using System.Net;
using System.Net.Http.Json;
using backend.Data;
using backend.Models.Plc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace backend.Tests;

public sealed class LineLifecycleApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public LineLifecycleApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateLine_DefaultsToDraftAndNotActive()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PostAsJsonAsync("/api/lines", new
        {
            lineNumber = 901,
            lineName = "Lifecycle Draft Line",
            productId = "123-456-78-9",
            recipeId = "RCP-901",
            machineId = "MX-901",
            operatorName = "operator-901",
            plcIp = "192.168.10.91",
            manufacturer = "AllenBradley",
            pollIntervalMs = 2000,
            isActive = true,
            lineLifecycleState = "Active",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LineConfigResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Draft", payload!.LineLifecycleState);
        Assert.False(payload.IsActive);
    }

    [Fact]
    public async Task UpdateLine_WithCommissioningLifecycleState_HonorsRequestedState()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var createResponse = await client.PostAsJsonAsync("/api/lines", new
        {
            lineNumber = 911,
            lineName = "Lifecycle Commissioning Line",
            productId = "123-456-78-3",
            recipeId = "RCP-911",
            machineId = "MX-911",
            operatorName = "operator-911",
            plcIp = "192.168.10.101",
            manufacturer = "AllenBradley",
            pollIntervalMs = 2000,
            isActive = false,
            lineLifecycleState = "Draft",
        });

        createResponse.EnsureSuccessStatusCode();

        var updateResponse = await client.PutAsJsonAsync("/api/lines/911", new
        {
            lineNumber = 911,
            lineName = "Lifecycle Commissioning Line",
            productId = "123-456-78-3",
            recipeId = "RCP-911",
            machineId = "MX-911",
            operatorName = "operator-911",
            plcIp = "192.168.10.101",
            manufacturer = "AllenBradley",
            pollIntervalMs = 2000,
            isActive = false,
            lineLifecycleState = "Commissioning",
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var payload = await updateResponse.Content.ReadFromJsonAsync<LineConfigResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Commissioning", payload!.LineLifecycleState);
        Assert.False(payload.IsActive);
    }

    [Fact]
    public async Task UpdateLine_WithConnectionChange_OnActiveLine_ResetsToDraft()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var createResponse = await client.PostAsJsonAsync("/api/lines", new
        {
            lineNumber = 902,
            lineName = "Lifecycle Reset Line",
            productId = "123-456-78-8",
            recipeId = "RCP-902",
            machineId = "MX-902",
            operatorName = "operator-902",
            plcIp = "192.168.10.92",
            manufacturer = "AllenBradley",
            pollIntervalMs = 2000,
            isActive = false,
            lineLifecycleState = "Draft",
        });

        createResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();
            var row = db.LineProtocolAssignments.Single(x => x.LineId == 902);
            row.LineLifecycleState = LineLifecycleState.Active;
            row.IsActive = true;
            db.SaveChanges();
        }

        var updateResponse = await client.PutAsJsonAsync("/api/lines/902", new
        {
            lineNumber = 902,
            lineName = "Lifecycle Reset Line",
            productId = "123-456-78-8",
            recipeId = "RCP-902",
            machineId = "MX-902",
            operatorName = "operator-902",
            plcIp = "192.168.10.99",
            manufacturer = "AllenBradley",
            pollIntervalMs = 2000,
            isActive = true,
            lineLifecycleState = "Active",
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var payload = await updateResponse.Content.ReadFromJsonAsync<LineConfigResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Draft", payload!.LineLifecycleState);
        Assert.False(payload.IsActive);
    }

    [Fact]
    public async Task ActivateCommissionedLine_WhenReadinessFails_ReturnsConflictAndMarksFailed()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var createResponse = await client.PostAsJsonAsync("/api/lines", new
        {
            lineNumber = 903,
            lineName = "Lifecycle Activation Line",
            productId = "123-456-78-7",
            recipeId = "RCP-903",
            machineId = "MX-903",
            operatorName = "operator-903",
            plcIp = "192.168.10.93",
            manufacturer = "AllenBradley",
            pollIntervalMs = 2000,
            isActive = false,
            lineLifecycleState = "Draft",
        });

        createResponse.EnsureSuccessStatusCode();

        var activateResponse = await client.PostAsJsonAsync("/api/admin/lines/903/commissioning-activate", new { });

        Assert.Equal(HttpStatusCode.Conflict, activateResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();
        var line = db.LineProtocolAssignments.Single(x => x.LineId == 903);
        Assert.Equal(LineLifecycleState.CommissioningFailed, line.LineLifecycleState);
        Assert.False(line.IsActive);
    }

    [Fact]
    public async Task CreateLine_WithUnsupportedManufacturer_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PostAsJsonAsync("/api/lines", new
        {
            lineNumber = 904,
            lineName = "Unsupported Driver Line",
            productId = "123-456-78-6",
            recipeId = "RCP-904",
            machineId = "MX-904",
            operatorName = "operator-904",
            plcIp = "192.168.10.94",
            manufacturer = "Siemens",
            pollIntervalMs = 2000,
            isActive = false,
            lineLifecycleState = "Draft",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateLine_WithDuplicateLineNumber_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        await client.PostAsJsonAsync("/api/lines", new
        {
            lineNumber = 905,
            lineName = "Primary Number Line",
            productId = "123-456-78-5",
            recipeId = "RCP-905",
            machineId = "MX-905",
            operatorName = "operator-905",
            plcIp = "192.168.10.95",
            manufacturer = "AllenBradley",
            pollIntervalMs = 2000,
            isActive = false,
            lineLifecycleState = "Draft",
        });

        await client.PostAsJsonAsync("/api/lines", new
        {
            lineNumber = 906,
            lineName = "Secondary Number Line",
            productId = "123-456-78-4",
            recipeId = "RCP-906",
            machineId = "MX-906",
            operatorName = "operator-906",
            plcIp = "192.168.10.96",
            manufacturer = "AllenBradley",
            pollIntervalMs = 2000,
            isActive = false,
            lineLifecycleState = "Draft",
        });

        var updateResponse = await client.PutAsJsonAsync("/api/lines/906", new
        {
            lineNumber = 905,
            lineName = "Secondary Number Line",
            productId = "123-456-78-4",
            recipeId = "RCP-906",
            machineId = "MX-906",
            operatorName = "operator-906",
            plcIp = "192.168.10.96",
            manufacturer = "AllenBradley",
            pollIntervalMs = 2000,
            isActive = false,
            lineLifecycleState = "Draft",
        });

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
    }

    [Fact]
    public async Task CreateLine_WithDuplicatePlcConnection_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var createFirst = await client.PostAsJsonAsync("/api/lines", new
        {
            lineNumber = 907,
            lineName = "Primary Connection Line",
            productId = "123-456-78-3",
            recipeId = "RCP-907",
            machineId = "MX-907",
            operatorName = "operator-907",
            plcIp = "192.168.10.97",
            manufacturer = "AllenBradley",
            pollIntervalMs = 2000,
            isActive = false,
            lineLifecycleState = "Draft",
        });

        if (createFirst.StatusCode != HttpStatusCode.Created)
        {
            Assert.Fail($"Expected first create to succeed but received {createFirst.StatusCode}.");
        }

        var createDuplicate = await client.PostAsJsonAsync("/api/lines", new
        {
            lineNumber = 908,
            lineName = "Duplicate Connection Line",
            productId = "123-456-78-2",
            recipeId = "RCP-908",
            machineId = "MX-908",
            operatorName = "operator-908",
            plcIp = "192.168.10.97",
            manufacturer = "AllenBradley",
            pollIntervalMs = 2000,
            isActive = false,
            lineLifecycleState = "Draft",
        });

        Assert.Equal(HttpStatusCode.Conflict, createDuplicate.StatusCode);
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

    private sealed class LineConfigResponse
    {
        public bool IsActive { get; set; }
        public string LineLifecycleState { get; set; } = string.Empty;
    }
}
