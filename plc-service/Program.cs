using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();
builder.Services.AddSingleton<WifiAgentService>();

var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "ok",
    service = "plantmonitor-privileged-agent",
    checkedAtUtc = DateTime.UtcNow,
}));

app.MapGet("/api/agent/wifi/status", async (WifiAgentService service, CancellationToken cancellationToken) =>
{
    var result = await service.GetStatusAsync(cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/api/agent/wifi/scan", async (WifiAgentService service, CancellationToken cancellationToken) =>
{
    var result = await service.ScanAsync(cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/agent/wifi/connect", async (WifiConnectRequest request, WifiAgentService service, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Ssid))
    {
        return Results.BadRequest(new
        {
            title = "Invalid request parameters.",
            status = 400,
            detail = "SSID is required.",
            code = "missing_ssid",
        });
    }

    var result = await service.ConnectAsync(request.Ssid.Trim(), request.Passphrase, cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/agent/wifi/disconnect", async (WifiAgentService service, CancellationToken cancellationToken) =>
{
    var result = await service.DisconnectAsync(cancellationToken);
    return Results.Ok(result);
});

app.Run();

file sealed class WifiAgentService
{
    public async Task<object> GetStatusAsync(CancellationToken cancellationToken)
    {
        var interfaceResult = await RunNetshAsync("wlan show interfaces", cancellationToken);
        if (!interfaceResult.Success)
        {
            return new
            {
                isConnected = false,
                ssid = (string?)null,
                bssid = (string?)null,
                signalQualityPercent = (int?)null,
                interfaceName = (string?)null,
                ipAddress = (string?)null,
                message = "Unable to query Wi-Fi interfaces.",
                errorCode = "wifi_interface_query_failed",
                checkedAtUtc = DateTime.UtcNow,
            };
        }

        var text = interfaceResult.StandardOutput;
        var state = ParseValue(text, "State") ?? "disconnected";
        var ssid = ParseSsid(text);
        var bssid = ParseValue(text, "BSSID");
        var signalQualityPercent = ParseSignalPercent(ParseValue(text, "Signal"));
        var interfaceName = ParseValue(text, "Name");
        var connected = string.Equals(state, "connected", StringComparison.OrdinalIgnoreCase);

        return new
        {
            isConnected = connected,
            ssid = connected ? ssid : null,
            bssid = connected ? bssid : null,
            signalQualityPercent = connected ? signalQualityPercent : null,
            interfaceName,
            ipAddress = connected ? ResolveIpv4ForInterface(interfaceName) : null,
            message = connected ? "Connected." : "No active Wi-Fi connection.",
            errorCode = (string?)null,
            checkedAtUtc = DateTime.UtcNow,
        };
    }

    public async Task<object> ScanAsync(CancellationToken cancellationToken)
    {
        var scanResult = await RunNetshAsync("wlan show networks mode=bssid", cancellationToken);
        if (!scanResult.Success)
        {
            return new
            {
                networks = Array.Empty<object>(),
                message = "Wi-Fi scan failed.",
                errorCode = "wifi_scan_failed",
                scannedAtUtc = DateTime.UtcNow,
            };
        }

        var status = await GetStatusAsync(cancellationToken);
        var statusJson = System.Text.Json.JsonSerializer.Serialize(status);
        var connectedSsid = ParseJsonProperty(statusJson, "ssid");

        var networks = ParseNetworks(scanResult.StandardOutput)
            .Select(network => new
            {
                ssid = network.Ssid,
                signalQualityPercent = network.SignalQualityPercent,
                security = network.Security,
                isConnected = string.Equals(network.Ssid, connectedSsid, StringComparison.OrdinalIgnoreCase),
            })
            .ToArray();

        return new
        {
            networks,
            message = "Scan completed.",
            errorCode = (string?)null,
            scannedAtUtc = DateTime.UtcNow,
        };
    }

    public async Task<object> ConnectAsync(string ssid, string passphrase, CancellationToken cancellationToken)
    {
        if (!await EnsureProfileAsync(ssid, passphrase, cancellationToken))
        {
            return new
            {
                status = "failed",
                message = "Failed to prepare Wi-Fi profile.",
                errorCode = "wifi_profile_write_failed",
                progressPercent = 0,
                changedAtUtc = DateTime.UtcNow,
            };
        }

        var result = await RunNetshAsync($"wlan connect name=\"{EscapeArgument(ssid)}\" ssid=\"{EscapeArgument(ssid)}\"", cancellationToken);
        if (!result.Success)
        {
            return new
            {
                status = "failed",
                message = "Wi-Fi connect command failed.",
                errorCode = "wifi_connect_failed",
                progressPercent = 100,
                changedAtUtc = DateTime.UtcNow,
            };
        }

        var verify = await GetStatusAsync(cancellationToken);
        var verifyJson = System.Text.Json.JsonSerializer.Serialize(verify);
        var connectedSsid = ParseJsonProperty(verifyJson, "ssid");
        var isConnected = string.Equals(ParseJsonProperty(verifyJson, "isConnected"), "true", StringComparison.OrdinalIgnoreCase)
            && string.Equals(connectedSsid, ssid, StringComparison.OrdinalIgnoreCase);

        return new
        {
            status = isConnected ? "connected" : "pending",
            message = isConnected
                ? $"Connected to {ssid}."
                : $"Connect command submitted for {ssid}; adapter reports pending state.",
            errorCode = (string?)null,
            progressPercent = 100,
            changedAtUtc = DateTime.UtcNow,
        };
    }

    public async Task<object> DisconnectAsync(CancellationToken cancellationToken)
    {
        var result = await RunNetshAsync("wlan disconnect", cancellationToken);
        if (!result.Success)
        {
            return new
            {
                status = "failed",
                message = "Wi-Fi disconnect command failed.",
                errorCode = "wifi_disconnect_failed",
                progressPercent = 100,
                changedAtUtc = DateTime.UtcNow,
            };
        }

        return new
        {
            status = "disconnected",
            message = "Wi-Fi disconnect command submitted.",
            errorCode = (string?)null,
            progressPercent = 100,
            changedAtUtc = DateTime.UtcNow,
        };
    }

    private static async Task<bool> EnsureProfileAsync(string ssid, string passphrase, CancellationToken cancellationToken)
    {
        var xml = BuildProfileXml(ssid, passphrase);
        var tempPath = Path.Combine(Path.GetTempPath(), $"plantmonitor-wifi-{Guid.NewGuid():N}.xml");

        try
        {
            await File.WriteAllTextAsync(tempPath, xml, Encoding.UTF8, cancellationToken);
            var addResult = await RunNetshAsync($"wlan add profile filename=\"{EscapeArgument(tempPath)}\" user=all", cancellationToken);
            return addResult.Success;
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }

    private static string BuildProfileXml(string ssid, string passphrase)
    {
        var escapedSsid = System.Security.SecurityElement.Escape(ssid) ?? ssid;
        var escapedPassphrase = System.Security.SecurityElement.Escape(passphrase) ?? passphrase;
        var auth = string.IsNullOrWhiteSpace(passphrase) ? "open" : "WPA2PSK";
        var encryption = string.IsNullOrWhiteSpace(passphrase) ? "none" : "AES";
        var keyType = string.IsNullOrWhiteSpace(passphrase) ? string.Empty : "<sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>" + escapedPassphrase + "</keyMaterial></sharedKey>";

        return $"<?xml version=\"1.0\"?>\n<WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\">\n  <name>{escapedSsid}</name>\n  <SSIDConfig>\n    <SSID>\n      <name>{escapedSsid}</name>\n    </SSID>\n  </SSIDConfig>\n  <connectionType>ESS</connectionType>\n  <connectionMode>auto</connectionMode>\n  <MSM>\n    <security>\n      <authEncryption>\n        <authentication>{auth}</authentication>\n        <encryption>{encryption}</encryption>\n        <useOneX>false</useOneX>\n      </authEncryption>\n      {keyType}\n    </security>\n  </MSM>\n</WLANProfile>";
    }

    private static async Task<NetshResult> RunNetshAsync(string arguments, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        try
        {
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;
            var success = process.ExitCode == 0;

            return new NetshResult(success, process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return new NetshResult(false, -1, string.Empty, ex.Message);
        }
    }

    private static string? ParseValue(string text, string key)
    {
        var regex = new Regex($"^{Regex.Escape(key)}\\s*:\\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        var match = regex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ParseSsid(string text)
    {
        var regex = new Regex("^\\s*SSID\\s*:\\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        foreach (Match match in regex.Matches(text))
        {
            if (match.Groups.Count < 2)
            {
                continue;
            }

            var value = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static int? ParseSignalPercent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var numeric = value.Replace("%", string.Empty, StringComparison.Ordinal).Trim();
        if (int.TryParse(numeric, out var parsed))
        {
            return Math.Clamp(parsed, 0, 100);
        }

        return null;
    }

    private static string? ResolveIpv4ForInterface(string? interfaceName)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            return null;
        }

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            var matched = interfaces.FirstOrDefault(network =>
                string.Equals(network.Name, interfaceName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(network.Description, interfaceName, StringComparison.OrdinalIgnoreCase));

            if (matched is null)
            {
                return null;
            }

            var properties = matched.GetIPProperties();
            var ipv4 = properties.UnicastAddresses
                .Select(address => address.Address)
                .FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(address));

            return ipv4?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyCollection<WifiNetwork> ParseNetworks(string text)
    {
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        var networks = new List<WifiNetwork>();

        string? currentSsid = null;
        string currentSecurity = "Unknown";
        int currentSignal = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("SSID ", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
            {
                if (!string.IsNullOrWhiteSpace(currentSsid))
                {
                    networks.Add(new WifiNetwork
                    {
                        Ssid = currentSsid,
                        SignalQualityPercent = currentSignal,
                        Security = currentSecurity,
                    });
                }

                var splitIndex = line.IndexOf(':');
                currentSsid = splitIndex >= 0 ? line[(splitIndex + 1)..].Trim() : null;
                currentSecurity = "Unknown";
                currentSignal = 0;
                continue;
            }

            if (line.StartsWith("Authentication", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
            {
                var splitIndex = line.IndexOf(':');
                if (splitIndex >= 0)
                {
                    currentSecurity = line[(splitIndex + 1)..].Trim();
                }
                continue;
            }

            if (line.StartsWith("Signal", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
            {
                var splitIndex = line.IndexOf(':');
                if (splitIndex >= 0)
                {
                    currentSignal = ParseSignalPercent(line[(splitIndex + 1)..].Trim()) ?? 0;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(currentSsid))
        {
            networks.Add(new WifiNetwork
            {
                Ssid = currentSsid,
                SignalQualityPercent = currentSignal,
                Security = currentSecurity,
            });
        }

        return networks
            .Where(network => !string.IsNullOrWhiteSpace(network.Ssid))
            .GroupBy(network => network.Ssid, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(network => network.SignalQualityPercent).First())
            .OrderByDescending(network => network.SignalQualityPercent)
            .ToArray();
    }

    private static string EscapeArgument(string input)
    {
        return input.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string? ParseJsonProperty(string json, string propertyName)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(propertyName, out var element))
            {
                return null;
            }

            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => element.GetString(),
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                _ => element.ToString(),
            };
        }
        catch
        {
            return null;
        }
    }
}

file sealed class WifiNetwork
{
    public required string Ssid { get; init; }
    public int SignalQualityPercent { get; init; }
    public string Security { get; init; } = "Unknown";
}

file sealed class WifiConnectRequest
{
    public string Ssid { get; init; } = string.Empty;
    public string Passphrase { get; init; } = string.Empty;
}

file sealed record NetshResult(bool Success, int ExitCode, string StandardOutput, string StandardError);
