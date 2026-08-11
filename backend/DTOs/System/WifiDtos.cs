namespace backend.DTOs.System;

public sealed class WifiStatusDto
{
    public bool IsConnected { get; init; }
    public string? Ssid { get; init; }
    public string? Bssid { get; init; }
    public int? SignalQualityPercent { get; init; }
    public string? InterfaceName { get; init; }
    public string? IpAddress { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public DateTime CheckedAtUtc { get; init; }
}

public sealed class WifiNetworkDto
{
    public required string Ssid { get; init; }
    public int SignalQualityPercent { get; init; }
    public string Security { get; init; } = "Unknown";
    public bool IsConnected { get; init; }
}

public sealed class WifiScanResponseDto
{
    public IReadOnlyCollection<WifiNetworkDto> Networks { get; init; } = [];
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public DateTime ScannedAtUtc { get; init; }
}

public sealed class WifiConnectRequestDto
{
    public string Ssid { get; init; } = string.Empty;
    public string Passphrase { get; init; } = string.Empty;
    public string ReauthToken { get; init; } = string.Empty;
}

public sealed class WifiDisconnectRequestDto
{
    public string ReauthToken { get; init; } = string.Empty;
}

public sealed class WifiActionResponseDto
{
    public required string Status { get; init; }
    public required string Message { get; init; }
    public string? ErrorCode { get; init; }
    public int? ProgressPercent { get; init; }
    public DateTime ChangedAtUtc { get; init; }
}
