using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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
    public async Task ConnectivityDiagnostics_WithOperatorToken_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "operator", "test");

        var response = await client.GetAsync("/api/admin/system/connectivity");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConnectivityDiagnostics_WithAdminToken_ReturnsSnapshot()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.GetAsync("/api/admin/system/connectivity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!["hostname"]?.GetValue<string>()));
        Assert.NotNull(payload["recommendedUrls"]?.AsArray());
    }

    [Fact]
    public async Task WifiStatus_WithOperatorToken_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "operator", "test");

        var response = await client.GetAsync("/api/admin/system/wifi/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WifiStatus_WithAdminToken_ReturnsStatusShape()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.GetAsync("/api/admin/system/wifi/status");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("Upstream service unavailable.", payload!["title"]?.GetValue<string>());
        Assert.Equal("agent_unreachable", payload["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task WifiScan_WithAdminToken_WhenAgentUnavailable_ReturnsBadGatewayProblemDetails()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.GetAsync("/api/admin/system/wifi/scan");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("Upstream service unavailable.", payload!["title"]?.GetValue<string>());
        Assert.Equal("agent_unreachable", payload["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task WifiConnect_RequiresValidReauthToken()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PostAsJsonAsync("/api/admin/system/wifi/connect", new
        {
            ssid = "Factory-Wifi-A",
            passphrase = "test-pass",
            reauthToken = "invalid-token",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtaCheck_WithOperatorToken_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "operator", "test");

        var response = await client.GetAsync("/api/admin/system/ota/check");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtaCheck_WithAdminToken_ReturnsStatusShape()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.GetAsync("/api/admin/system/ota/check");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!["status"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(payload["currentVersion"]?.GetValue<string>()));
        Assert.NotNull(payload["hasUpdate"]);
    }

    [Fact]
    public async Task AdminReauth_WithWrongPassword_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PostAsJsonAsync("/api/admin/system/reauth", new
        {
            password = "wrong-password",
            scope = "ota-apply",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtaPrepareApply_RequiresValidReauthToken()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var rejected = await client.PostAsJsonAsync("/api/admin/system/ota/prepare-apply", new
        {
            targetVersion = "0.1.0",
            reauthToken = "invalid-token",
        });

        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);

        var reauthResponse = await client.PostAsJsonAsync("/api/admin/system/reauth", new
        {
            password = "test",
            scope = "ota-apply",
        });

        reauthResponse.EnsureSuccessStatusCode();
        var reauthPayload = await reauthResponse.Content.ReadFromJsonAsync<JsonObject>();
        var token = reauthPayload?["token"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var accepted = await client.PostAsJsonAsync("/api/admin/system/ota/prepare-apply", new
        {
            targetVersion = "0.1.0",
            reauthToken = token,
        });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var payload = await accepted.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(payload);
        Assert.Equal("ready_for_apply", payload!["status"]?.GetValue<string>());
    }

    [Fact]
    public async Task OtaStage_RequiresValidReauthToken()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var rejected = await client.PostAsJsonAsync("/api/admin/system/ota/stage", new
        {
            targetVersion = "0.1.0",
            packageUrl = "C:\\temp\\package.zip",
            expectedSha256 = "",
            reauthToken = "invalid-token",
        });

        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
    }

    [Fact]
    public async Task OtaStage_WithValidReauthToken_StagesLocalFileAndVerifiesHash()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var tempPackagePath = Path.Combine(Path.GetTempPath(), $"plantmonitor-ota-{Guid.NewGuid():N}.bin");
        await File.WriteAllTextAsync(tempPackagePath, "plantmonitor-ota-test", Encoding.UTF8);

        try
        {
            var fileBytes = await File.ReadAllBytesAsync(tempPackagePath);
            var expectedSha = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();

            var reauthResponse = await client.PostAsJsonAsync("/api/admin/system/reauth", new
            {
                password = "test",
                scope = "ota-stage",
            });
            reauthResponse.EnsureSuccessStatusCode();

            var reauthPayload = await reauthResponse.Content.ReadFromJsonAsync<JsonObject>();
            var token = reauthPayload?["token"]?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(token));

            var staged = await client.PostAsJsonAsync("/api/admin/system/ota/stage", new
            {
                targetVersion = "0.1.0",
                packageUrl = tempPackagePath,
                expectedSha256 = expectedSha,
                reauthToken = token,
            });

            Assert.Equal(HttpStatusCode.OK, staged.StatusCode);

            var payload = await staged.Content.ReadFromJsonAsync<JsonObject>();
            Assert.NotNull(payload);
            Assert.Equal("staged", payload!["status"]?.GetValue<string>());
            Assert.Equal(true, payload["isChecksumMatch"]?.GetValue<bool>());
            Assert.Equal(expectedSha, payload["sha256"]?.GetValue<string>());
            Assert.False(string.IsNullOrWhiteSpace(payload["operationId"]?.GetValue<string>()));
        }
        finally
        {
            try
            {
                File.Delete(tempPackagePath);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task OtaApply_RequiresValidReauthToken()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var response = await client.PostAsJsonAsync("/api/admin/system/ota/apply", new
        {
            stageOperationId = "missing-stage-operation",
            reauthToken = "invalid-token",
            forceHealthFailure = false,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtaApply_WithValidReauthToken_ExecutesRollbackFlowAndExposesStatus()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client, "test", "test");

        var tempPackagePath = Path.Combine(Path.GetTempPath(), $"plantmonitor-ota-apply-{Guid.NewGuid():N}.bin");
        await File.WriteAllTextAsync(tempPackagePath, "plantmonitor-ota-apply-test", Encoding.UTF8);

        try
        {
            var stageToken = await IssueReauthTokenAsync(client, "ota-stage");

            var stageResponse = await client.PostAsJsonAsync("/api/admin/system/ota/stage", new
            {
                targetVersion = "0.1.1",
                packageUrl = tempPackagePath,
                expectedSha256 = "",
                reauthToken = stageToken,
            });

            stageResponse.EnsureSuccessStatusCode();
            var stagePayload = await stageResponse.Content.ReadFromJsonAsync<JsonObject>();
            var stageOperationId = stagePayload?["operationId"]?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(stageOperationId));

            var applyToken = await IssueReauthTokenAsync(client, "ota-apply");

            var applyResponse = await client.PostAsJsonAsync("/api/admin/system/ota/apply", new
            {
                stageOperationId,
                reauthToken = applyToken,
                forceHealthFailure = true,
            });

            applyResponse.EnsureSuccessStatusCode();
            var applyPayload = await applyResponse.Content.ReadFromJsonAsync<JsonObject>();
            Assert.NotNull(applyPayload);
            Assert.Equal("rolled_back", applyPayload!["status"]?.GetValue<string>());
            Assert.Equal(true, applyPayload["rolledBack"]?.GetValue<bool>());
            var applyOperationId = applyPayload["operationId"]?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(applyOperationId));

            var statusResponse = await client.GetAsync($"/api/admin/system/ota/apply/{applyOperationId}");
            statusResponse.EnsureSuccessStatusCode();

            var statusPayload = await statusResponse.Content.ReadFromJsonAsync<JsonObject>();
            Assert.NotNull(statusPayload);
            Assert.Equal(applyOperationId, statusPayload!["operationId"]?.GetValue<string>());
            Assert.Equal("rolled_back", statusPayload["status"]?.GetValue<string>());
            Assert.Equal(true, statusPayload["rolledBack"]?.GetValue<bool>());
        }
        finally
        {
            try
            {
                File.Delete(tempPackagePath);
            }
            catch
            {
            }
        }
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

    private static async Task<string> IssueReauthTokenAsync(HttpClient client, string scope)
    {
        var reauthResponse = await client.PostAsJsonAsync("/api/admin/system/reauth", new
        {
            password = "test",
            scope,
        });

        reauthResponse.EnsureSuccessStatusCode();
        var payload = await reauthResponse.Content.ReadFromJsonAsync<JsonObject>();
        var token = payload?["token"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }
}
