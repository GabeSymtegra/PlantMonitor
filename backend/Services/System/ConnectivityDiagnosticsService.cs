using System.Net;
using System.Net.NetworkInformation;
using backend.DTOs.System;
using backend.Interfaces;

namespace backend.Services.Host;

public sealed class ConnectivityDiagnosticsService : IConnectivityDiagnosticsService
{
    private readonly IConfiguration _configuration;

    public ConnectivityDiagnosticsService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public HostConnectivitySnapshotDto BuildSnapshot(string scheme, int port)
    {
        var normalizedScheme = string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase)
            ? "https"
            : "http";

        var serviceBind = ResolveServiceBind(port);
        var allowedHosts = _configuration["AllowedHosts"]?.Trim();
        var normalizedAllowedHosts = string.IsNullOrWhiteSpace(allowedHosts) ? "*" : allowedHosts;
        var isLanDeployment = _configuration.GetValue<bool>("App:IsLanDeployment");
        var corsOrigins = _configuration.GetSection("App:CorsOrigins").Get<string[]>() ?? [];

        var interfaceRows = GetActiveInterfaces();
        var recommendedUrls = BuildRecommendedUrls(normalizedScheme, port, interfaceRows);

        var warnings = new List<string>();

        if (IsLocalhostOnlyBinding(serviceBind))
        {
            warnings.Add("Service binding appears localhost-only. Remote factory Wi-Fi devices may be unable to open the dashboard.");
        }

        if (!isLanDeployment)
        {
            warnings.Add("App:IsLanDeployment is disabled. Verify production network policy before allowing wireless client access.");
        }

        if (corsOrigins.Length == 0 && !isLanDeployment)
        {
            warnings.Add("No App:CorsOrigins are configured for production mode. Browser clients on factory Wi-Fi may be blocked.");
        }

        if (interfaceRows.Count == 0)
        {
            warnings.Add("No active non-loopback IPv4 interfaces were detected.");
        }

        return new HostConnectivitySnapshotDto
        {
            Hostname = Environment.MachineName,
            AccessMode = IsLocalhostOnlyBinding(serviceBind) ? "LocalOnly" : "LanCompatible",
            ServiceBind = serviceBind,
            AllowedHosts = normalizedAllowedHosts,
            LanDeploymentEnabled = isLanDeployment,
            RecommendedUrls = recommendedUrls,
            ActiveInterfaces = interfaceRows,
            Warnings = warnings,
            GeneratedAtUtc = DateTime.UtcNow,
        };
    }

    private string ResolveServiceBind(int port)
    {
        var bind = _configuration["ASPNETCORE_URLS"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

        if (!string.IsNullOrWhiteSpace(bind))
        {
            return bind.Trim();
        }

        return $"http://0.0.0.0:{port}";
    }

    private static List<NetworkInterfaceSummaryDto> GetActiveInterfaces()
    {
        var rows = new List<NetworkInterfaceSummaryDto>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback
                || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            var properties = networkInterface.GetIPProperties();
            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    continue;
                }

                var ipAddress = unicast.Address.ToString();
                rows.Add(new NetworkInterfaceSummaryDto
                {
                    Name = networkInterface.Name,
                    Type = networkInterface.NetworkInterfaceType.ToString(),
                    IpAddress = ipAddress,
                    IsWireless = networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211,
                    IsPrivateAddress = IsPrivateIpv4(unicast.Address),
                });
            }
        }

        return rows
            .OrderByDescending(x => x.IsPrivateAddress)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyCollection<string> BuildRecommendedUrls(
        string scheme,
        int port,
        IReadOnlyCollection<NetworkInterfaceSummaryDto> interfaces)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"{scheme}://{Environment.MachineName}:{port}",
            $"{scheme}://localhost:{port}",
        };

        foreach (var row in interfaces.Where(x => x.IsPrivateAddress))
        {
            urls.Add($"{scheme}://{row.IpAddress}:{port}");
        }

        return urls.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsLocalhostOnlyBinding(string serviceBind)
    {
        if (string.IsNullOrWhiteSpace(serviceBind))
        {
            return false;
        }

        var segments = serviceBind
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var segment in segments)
        {
            if (segment.Contains("0.0.0.0", StringComparison.OrdinalIgnoreCase)
                || segment.Contains("[::]", StringComparison.OrdinalIgnoreCase)
                || segment.Contains("+", StringComparison.OrdinalIgnoreCase)
                || segment.Contains("*", StringComparison.OrdinalIgnoreCase)
                || segment.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return segments.All(segment =>
            segment.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("[::1]", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false;
        }

        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
