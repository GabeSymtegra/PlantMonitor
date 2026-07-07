using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using backend.DTOs.Plc;
using backend.Interfaces.Plc;
using backend.Services.Plc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace backend.Tests;

public sealed class PlcConnectionServiceTests
{
    [Fact]
    public async Task TestConnection_WithInvalidIp_ReturnsFailure()
    {
        var service = new PlcConnectionService(new FakePlcDriver("AllenBradley"));

        var result = await service.TestConnectionAsync(new PlcConnectionRequest
        {
            Driver = "AllenBradley",
            IpAddress = "not-an-ip",
        });

        Assert.False(result.IsConnected);
        Assert.Equal("Invalid IP address. Enter a valid IPv4 address.", result.Message);
    }

    [Fact]
    public async Task TestConnection_WithUnknownDriver_ReturnsFailure()
    {
        var service = new PlcConnectionService(new FakePlcDriver("AllenBradley"));

        var result = await service.TestConnectionAsync(new PlcConnectionRequest
        {
            Driver = "Unknown",
            IpAddress = "192.168.1.10",
        });

        Assert.False(result.IsConnected);
        Assert.Equal("Wrong driver selected. Choose AllenBradley or Siemens.", result.Message);
    }

    [Fact]
    public async Task TestConnection_WithKnownDriver_DelegatesToDriver()
    {
        var driver = new FakePlcDriver("AllenBradley")
        {
            ResultFactory = request => new PlcConnectionResult
            {
                IsConnected = true,
                Driver = request.Driver,
                IpAddress = request.IpAddress,
                ControllerName = "Test Controller",
                Firmware = "v1.2.3",
                ResponseTimeMs = 42,
                Message = "Connected to test PLC.",
            },
        };

        var service = new PlcConnectionService(driver);

        var result = await service.TestConnectionAsync(new PlcConnectionRequest
        {
            Driver = "AllenBradley",
            IpAddress = "192.168.1.10",
        });

        Assert.True(result.IsConnected);
        Assert.Equal("Test Controller", result.ControllerName);
        Assert.Equal("v1.2.3", result.Firmware);
        Assert.Equal(42, result.ResponseTimeMs);
        Assert.Equal(1, driver.CallCount);
    }

    private sealed class FakePlcDriver : IPlcDriver
    {
        public FakePlcDriver(string driverName)
        {
            DriverName = driverName;
        }

        public string DriverName { get; }

        public int CallCount { get; private set; }

        public Func<PlcConnectionRequest, PlcConnectionResult>? ResultFactory { get; init; }

        public Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, CancellationToken cancellationToken = default)
        {
            CallCount++;

            var request = new PlcConnectionRequest
            {
                Driver = DriverName,
                IpAddress = ipAddress,
            };

            var result = ResultFactory?.Invoke(request) ?? new PlcConnectionResult
            {
                IsConnected = true,
                Driver = DriverName,
                IpAddress = ipAddress,
                Message = "Connected.",
            };

            return Task.FromResult(result);
        }

        public Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsAsync(
            string ipAddress,
            string? search = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<PlcTagBrowseItemDto>>([]);
        }

        public Task<PlcTagReadResultDto> ReadTagAsync(
            string ipAddress,
            string tagName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PlcTagReadResultDto
            {
                Name = tagName,
                LastReadUtc = DateTime.UtcNow,
            });
        }

        public Task<IReadOnlyCollection<PlcTagReadResultDto>> ReadTagsAsync(
            string ipAddress,
            IReadOnlyCollection<string> tagNames,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<PlcTagReadResultDto>>([]);
        }

        public Task<PlcTagWriteResultDto> WriteTagAsync(
            string ipAddress,
            string tagName,
            string value,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PlcTagWriteResultDto
            {
                Name = tagName,
                Success = true,
                AttemptedAtUtc = DateTime.UtcNow,
            });
        }
    }
}

public sealed class PlcConnectionApiTests : IClassFixture<PlcConnectionApiFactory>
{
    private readonly PlcConnectionApiFactory _factory;

    public PlcConnectionApiTests(PlcConnectionApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TestConnection_WithAdminToken_ReturnsConnectionResult()
    {
        var client = _factory.CreateClient();
        var token = await GetAccessToken(client, "test", "test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/plc/test-connection", new
        {
            driver = "AllenBradley",
            ipAddress = "192.168.1.10",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.True(payload?["isConnected"]?.GetValue<bool>());
        Assert.Equal("AllenBradley", payload?["driver"]?.GetValue<string>());
        Assert.Equal("192.168.1.10", payload?["ipAddress"]?.GetValue<string>());
        Assert.Equal("Test Controller", payload?["controllerName"]?.GetValue<string>());
        Assert.Equal("v9.9.9", payload?["firmware"]?.GetValue<string>());
        Assert.Equal(88, payload?["responseTimeMs"]?.GetValue<int>());
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

public sealed class PlcConnectionApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPlcConnectionService>();
            services.AddSingleton<IPlcConnectionService>(new FakeConnectionService());
        });
    }

    private sealed class FakeConnectionService : IPlcConnectionService
    {
        public Task<PlcConnectionResult> TestConnectionAsync(PlcConnectionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PlcConnectionResult
            {
                IsConnected = true,
                Driver = request.Driver,
                IpAddress = request.IpAddress,
                ControllerName = "Test Controller",
                Firmware = "v9.9.9",
                ResponseTimeMs = 88,
                Message = "Connected to test PLC.",
            });
        }

        public bool TryResolveDriver(string? driverName, out IPlcDriver? driver, out string? errorMessage)
        {
            driver = null;
            errorMessage = "Wrong driver selected. Choose AllenBradley or Siemens.";
            return false;
        }

        public bool IsValidIpAddress(string? ipAddress)
        {
            return true;
        }
    }
}