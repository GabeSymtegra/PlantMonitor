using backend.DTOs.System;

namespace backend.Interfaces;

public interface IPrivilegedHostAgentClient
{
    Task<WifiStatusDto> GetWifiStatusAsync(CancellationToken cancellationToken = default);
    Task<WifiScanResponseDto> ScanWifiAsync(CancellationToken cancellationToken = default);
    Task<WifiActionResponseDto> ConnectWifiAsync(string ssid, string passphrase, CancellationToken cancellationToken = default);
    Task<WifiActionResponseDto> DisconnectWifiAsync(CancellationToken cancellationToken = default);
}
