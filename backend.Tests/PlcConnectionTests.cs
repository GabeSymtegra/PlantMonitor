using System.Net;
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
        Assert.Equal("Wrong driver selected. Choose AllenBradley.", result.Message);
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

        public Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, PlcConnectionOptionsDto? options, CancellationToken cancellationToken = default)
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

        public Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, CancellationToken cancellationToken = default)
        {
            return TestConnectionAsync(ipAddress, null, cancellationToken);
        }

        public Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsAsync(
            string ipAddress,
            PlcConnectionOptionsDto? options,
            string? search = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<PlcTagBrowseItemDto>>([]);
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
            PlcConnectionOptionsDto? options,
            string tagName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PlcTagReadResultDto
            {
                Name = tagName,
                LastReadUtc = DateTime.UtcNow,
            });
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
            PlcConnectionOptionsDto? options,
            IReadOnlyCollection<string> tagNames,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<PlcTagReadResultDto>>([]);
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
        await LoginAsync(client, "test", "test");

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

    [Fact]
    public async Task TestConnection_WithDriverFailure_ReturnsBadGateway()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PostAsJsonAsync("/api/admin/plc/test-connection", new
        {
            driver = "AllenBradley",
            ipAddress = "192.168.1.99",
        });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceTagCatalog_WithUnknownLogicalKey_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PutAsJsonAsync("/api/admin/lines/101/tag-catalog", new
        {
            driver = "AllenBradley",
            tags = new[]
            {
                new
                {
                    logicalKey = "unknown_key",
                    displayName = "Unknown",
                    driver = "AllenBradley",
                    plcAddress = "Machine.LineSpeed",
                    dataType = "real",
                    scale = 1.0m,
                    isEnabled = true,
                    isRequired = true,
                    readFrequencyMs = 1000,
                },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BrowseTags_WithAdminToken_ReturnsResolvedDriverTags()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PostAsJsonAsync("/api/admin/plc/browse-tags", new
        {
            driver = "AllenBradley",
            ipAddress = "192.168.1.10",
            search = "Machine",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonArray>();
        Assert.NotNull(payload);
        Assert.Contains(payload!, item => item?["name"]?.GetValue<string>() == "Machine.LineSpeed");
    }

    [Fact]
    public async Task ReadTag_WithAdminToken_ReturnsResolvedDriverValue()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PostAsJsonAsync("/api/admin/plc/read-tag", new
        {
            driver = "AllenBradley",
            ipAddress = "192.168.1.10",
            tagName = "Machine.LineSpeed",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("Machine.LineSpeed", payload?["name"]?.GetValue<string>());
        Assert.Equal("125.4", payload?["value"]?.GetValue<string>());
    }

    [Fact]
    public async Task AutoMapTags_WithAdminToken_ReturnsSuggestionPayload()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PostAsJsonAsync("/api/admin/plc/auto-map-tags", new
        {
            driver = "AllenBradley",
            ipAddress = "192.168.1.10",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("AllenBradley", payload?["driver"]?.GetValue<string>());
        Assert.True(payload?["scannedTagCount"]?.GetValue<int>() > 0);
        Assert.NotNull(payload?["suggestedMappings"]?.AsArray());
        Assert.NotNull(payload?["missingLogicalKeys"]?.AsArray());
    }

    [Fact]
    public async Task CommissioningCheck_WithAdminToken_ReturnsReadinessPayload()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var upsertResponse = await client.PutAsJsonAsync("/api/admin/lines/101/protocol-assignment", new
        {
            manufacturer = "AllenBradley",
            presetName = "BasicStatus",
            presetVersion = 1,
            pollIntervalMs = 1500,
        });

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);

        var response = await client.GetAsync("/api/admin/lines/101/commissioning-check");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal(101, payload?["lineId"]?.GetValue<int>());
        Assert.Equal("AllenBradley", payload?["manufacturer"]?.GetValue<string>());
        Assert.NotNull(payload?["missingRequiredTagKeys"]?.AsArray());
        Assert.NotNull(payload?["issues"]?.AsArray());
    }

    [Fact]
    public async Task CommissioningCheck_WithCompleteCatalogAndReadableTags_ReturnsReady()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var upsertResponse = await client.PutAsJsonAsync("/api/admin/lines/101/protocol-assignment", new
        {
            manufacturer = "AllenBradley",
            presetName = "BasicStatus",
            presetVersion = 1,
            pollIntervalMs = 1500,
        });

        upsertResponse.EnsureSuccessStatusCode();

        var catalogResponse = await client.PutAsJsonAsync("/api/admin/lines/101/tag-catalog", new
        {
            driver = "AllenBradley",
            tags = BuildRequiredCatalog("real"),
        });

        catalogResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/admin/lines/101/commissioning-check");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.True(payload?["isReady"]?.GetValue<bool>());
        Assert.Equal(11, payload?["requiredTagCount"]?.GetValue<int>());
        Assert.Equal(11, payload?["mappedRequiredTagCount"]?.GetValue<int>());
    }

    [Fact]
    public async Task CommissioningCheck_WithBooleanNumericMapping_ReturnsDetailedTypeIssue()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var upsertResponse = await client.PutAsJsonAsync("/api/admin/lines/101/protocol-assignment", new
        {
            manufacturer = "AllenBradley",
            presetName = "BasicStatus",
            presetVersion = 1,
            pollIntervalMs = 1500,
        });

        upsertResponse.EnsureSuccessStatusCode();

        var catalogResponse = await client.PutAsJsonAsync("/api/admin/lines/101/tag-catalog", new
        {
            driver = "AllenBradley",
            tags = BuildRequiredCatalog("bool"),
        });

        catalogResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/admin/lines/101/commissioning-check");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.False(payload?["isReady"]?.GetValue<bool>());

        var issues = payload?["issues"]?.AsArray();
        Assert.NotNull(issues);
        Assert.Contains(issues!, issue =>
            (issue?.GetValue<string>() ?? string.Empty).Contains("production_length", StringComparison.OrdinalIgnoreCase)
            && (issue?.GetValue<string>() ?? string.Empty).Contains("numeric data type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ActivateCommissionedLine_WithPassingValidation_ReturnsActive()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var upsertResponse = await client.PutAsJsonAsync("/api/admin/lines/101/protocol-assignment", new
        {
            manufacturer = "AllenBradley",
            presetName = "BasicStatus",
            presetVersion = 1,
            pollIntervalMs = 1500,
        });

        upsertResponse.EnsureSuccessStatusCode();

        var catalogResponse = await client.PutAsJsonAsync("/api/admin/lines/101/tag-catalog", new
        {
            driver = "AllenBradley",
            tags = BuildRequiredCatalog("real"),
        });

        catalogResponse.EnsureSuccessStatusCode();

        var activateResponse = await client.PostAsJsonAsync("/api/admin/lines/101/commissioning-activate", new { });
        activateResponse.EnsureSuccessStatusCode();

        var payload = await activateResponse.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal(101, payload?["lineId"]?.GetValue<int>());
        Assert.Equal("Active", payload?["lineLifecycleState"]?.GetValue<string>());
    }

    [Fact]
    public async Task UpsertProtocolAssignment_WithConnectionSettings_PersistsOptions()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var upsertResponse = await client.PutAsJsonAsync("/api/admin/lines/101/protocol-assignment", new
        {
            manufacturer = "AllenBradley",
            presetName = "BasicStatus",
            presetVersion = 1,
            pollIntervalMs = 1750,
            routePath = "1,2",
            processorType = "CompactLogix",
            connectionTimeoutMs = 4500,
            readTimeoutMs = 4800,
            retryCount = 2,
            retryDelayMs = 400,
        });

        upsertResponse.EnsureSuccessStatusCode();

        var getResponse = await client.GetAsync("/api/admin/lines/101/protocol-assignment");
        getResponse.EnsureSuccessStatusCode();

        var payload = await getResponse.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("1,2", payload?["routePath"]?.GetValue<string>());
        Assert.Equal("CompactLogix", payload?["processorType"]?.GetValue<string>());
        Assert.Equal(4500, payload?["connectionTimeoutMs"]?.GetValue<int>());
        Assert.Equal(4800, payload?["readTimeoutMs"]?.GetValue<int>());
        Assert.Equal(2, payload?["retryCount"]?.GetValue<int>());
        Assert.Equal(400, payload?["retryDelayMs"]?.GetValue<int>());
    }

    [Fact]
    public async Task UpsertProtocolAssignment_WithInvalidRoutePath_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PutAsJsonAsync("/api/admin/lines/101/protocol-assignment", new
        {
            manufacturer = "AllenBradley",
            presetName = "BasicStatus",
            presetVersion = 1,
            pollIntervalMs = 1500,
            routePath = "1/a",
            processorType = "ControlLogix",
            connectionTimeoutMs = 3000,
            readTimeoutMs = 3000,
            retryCount = 1,
            retryDelayMs = 250,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static object[] BuildRequiredCatalog(string productionLengthDataType)
    {
        return
        [
            CreateTag("line_id", "Program:LineData.LineId", "int"),
            CreateTag("product_id", "Program:LineData.ProductId", "string"),
            CreateTag("control_mode", "Program:LineData.ControlMode", "int"),
            CreateTag("machine_state", "Program:LineData.MachineState", "string"),
            CreateTag("production_length", "Program:LineData.ProductionLength", productionLengthDataType),
            CreateTag("bare_setpoint", "Program:LineData.BareSetpoint", "real"),
            CreateTag("bare_actual", "Program:LineData.BareActual", "real"),
            CreateTag("hot_setpoint", "Program:LineData.HotSetpoint", "real"),
            CreateTag("hot_actual", "Program:LineData.HotActual", "real"),
            CreateTag("cold_setpoint", "Program:LineData.ColdSetpoint", "real"),
            CreateTag("cold_actual", "Program:LineData.ColdActual", "real"),
        ];
    }

    private static object CreateTag(string logicalKey, string plcAddress, string dataType)
    {
        return new
        {
            logicalKey,
            displayName = logicalKey,
            driver = "AllenBradley",
            plcAddress,
            dataType,
            scale = 1.0m,
            isEnabled = true,
            isRequired = true,
            readFrequencyMs = 1000,
        };
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

public sealed class PlcConnectionApiFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPlcConnectionService>();
            services.AddSingleton<IPlcConnectionService>(new FakeConnectionService());
        });
    }

    private sealed class FakeConnectionService : IPlcConnectionService
    {
        private readonly IPlcDriver _driver = new FakeResolvedDriver();

        public Task<PlcConnectionResult> TestConnectionAsync(PlcConnectionRequest request, CancellationToken cancellationToken = default)
        {
            if (string.Equals(request.IpAddress, "192.168.1.99", StringComparison.Ordinal))
            {
                return Task.FromResult(new PlcConnectionResult
                {
                    IsConnected = false,
                    Driver = request.Driver,
                    IpAddress = request.IpAddress,
                    Message = "Driver timeout while connecting to PLC.",
                });
            }

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
            if (string.Equals(driverName, _driver.DriverName, StringComparison.OrdinalIgnoreCase))
            {
                driver = _driver;
                errorMessage = null;
                return true;
            }

            driver = null;
            errorMessage = "Wrong driver selected. Choose AllenBradley.";
            return false;
        }

        public bool IsValidIpAddress(string? ipAddress)
        {
            return true;
        }
    }

    private sealed class FakeResolvedDriver : IPlcDriver
    {
        public string DriverName => "AllenBradley";

        public Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, PlcConnectionOptionsDto? options, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PlcConnectionResult
            {
                IsConnected = true,
                Driver = DriverName,
                IpAddress = ipAddress,
                ControllerName = "Test Controller",
                Firmware = "v9.9.9",
                ResponseTimeMs = 88,
                Message = "Connected to test PLC.",
            });
        }

        public Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, CancellationToken cancellationToken = default)
        {
            return TestConnectionAsync(ipAddress, null, cancellationToken);
        }

        public Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsAsync(string ipAddress, PlcConnectionOptionsDto? options, string? search = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<PlcTagBrowseItemDto>>(
            [
                new PlcTagBrowseItemDto
                {
                    Name = "Machine.LineSpeed",
                    DataType = "real",
                    IsFolder = false,
                    ParentPath = "Machine",
                    CanRead = true,
                    CanWrite = false,
                },
            ]);
        }

        public Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsAsync(string ipAddress, string? search = null, CancellationToken cancellationToken = default)
        {
            return BrowseTagsAsync(ipAddress, null, search, cancellationToken);
        }

        public Task<PlcTagReadResultDto> ReadTagAsync(string ipAddress, PlcConnectionOptionsDto? options, string tagName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BuildReadResult(tagName));
        }

        public Task<PlcTagReadResultDto> ReadTagAsync(string ipAddress, string tagName, CancellationToken cancellationToken = default)
        {
            return ReadTagAsync(ipAddress, null, tagName, cancellationToken);
        }

        public Task<IReadOnlyCollection<PlcTagReadResultDto>> ReadTagsAsync(string ipAddress, PlcConnectionOptionsDto? options, IReadOnlyCollection<string> tagNames, CancellationToken cancellationToken = default)
        {
            var results = tagNames.Select(BuildReadResult).ToArray();

            return Task.FromResult<IReadOnlyCollection<PlcTagReadResultDto>>(results);
        }

        public Task<IReadOnlyCollection<PlcTagReadResultDto>> ReadTagsAsync(string ipAddress, IReadOnlyCollection<string> tagNames, CancellationToken cancellationToken = default)
        {
            return ReadTagsAsync(ipAddress, null, tagNames, cancellationToken);
        }

        private static PlcTagReadResultDto BuildReadResult(string tagName)
        {
            var value = tagName switch
            {
                var name when name.Contains("ControlMode", StringComparison.OrdinalIgnoreCase) => "1",
                var name when name.Contains("MachineState", StringComparison.OrdinalIgnoreCase) => "Running",
                var name when name.Contains("ProductId", StringComparison.OrdinalIgnoreCase) => "P-101",
                var name when name.Contains("LineId", StringComparison.OrdinalIgnoreCase) => "101",
                var name when name.Contains("ProductionLength", StringComparison.OrdinalIgnoreCase) => "125.4",
                var name when name.Contains("Setpoint", StringComparison.OrdinalIgnoreCase) => "10.5",
                var name when name.Contains("Actual", StringComparison.OrdinalIgnoreCase) => "10.2",
                _ => "125.4",
            };

            return new PlcTagReadResultDto
            {
                Name = tagName,
                DataType = "real",
                Value = value,
                LastReadUtc = DateTime.UtcNow,
                CanRead = true,
                CanWrite = false,
            };
        }

        public Task<PlcTagWriteResultDto> WriteTagAsync(string ipAddress, string tagName, string value, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PlcTagWriteResultDto
            {
                Name = tagName,
                Success = false,
                AttemptedAtUtc = DateTime.UtcNow,
                Error = "Write is disabled in this test driver.",
            });
        }
    }
}