namespace backend.DTOs.System;

public sealed class HostConnectivitySnapshotDto
{
    public required string Hostname { get; init; }
    public required string AccessMode { get; init; }
    public required string ServiceBind { get; init; }
    public required string AllowedHosts { get; init; }
    public required bool LanDeploymentEnabled { get; init; }
    public required IReadOnlyCollection<string> RecommendedUrls { get; init; }
    public required IReadOnlyCollection<NetworkInterfaceSummaryDto> ActiveInterfaces { get; init; }
    public required IReadOnlyCollection<string> Warnings { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
}

public sealed class NetworkInterfaceSummaryDto
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string IpAddress { get; init; }
    public bool IsWireless { get; init; }
    public bool IsPrivateAddress { get; init; }
}
