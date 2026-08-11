using System.Net.Http.Json;
using backend.DTOs.System;
using backend.Interfaces;

namespace backend.Services.Host;

public sealed class PrivilegedHostAgentClient : IPrivilegedHostAgentClient
{
    private readonly HttpClient _httpClient;

    public PrivilegedHostAgentClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WifiStatusDto> GetWifiStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/agent/wifi/status", cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<WifiStatusDto>(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new WifiStatusDto
                {
                    IsConnected = false,
                    Message = result?.Message ?? "Privileged host agent returned an error for Wi-Fi status.",
                    ErrorCode = result?.ErrorCode ?? "agent_status_error",
                    CheckedAtUtc = DateTime.UtcNow,
                };
            }

            if (result is not null)
            {
                return result;
            }
        }
        catch
        {
        }

        return new WifiStatusDto
        {
            IsConnected = false,
            Message = "Privileged host agent is unreachable.",
            ErrorCode = "agent_unreachable",
            CheckedAtUtc = DateTime.UtcNow,
        };
    }

    public async Task<WifiScanResponseDto> ScanWifiAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/agent/wifi/scan", cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<WifiScanResponseDto>(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new WifiScanResponseDto
                {
                    Networks = [],
                    Message = result?.Message ?? "Privileged host agent returned an error for Wi-Fi scan.",
                    ErrorCode = result?.ErrorCode ?? "agent_scan_error",
                    ScannedAtUtc = DateTime.UtcNow,
                };
            }

            if (result is not null)
            {
                return result;
            }
        }
        catch
        {
        }

        return new WifiScanResponseDto
        {
            Networks = [],
            Message = "Privileged host agent is unreachable.",
            ErrorCode = "agent_unreachable",
            ScannedAtUtc = DateTime.UtcNow,
        };
    }

    public async Task<WifiActionResponseDto> ConnectWifiAsync(string ssid, string passphrase, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/agent/wifi/connect", new
            {
                ssid,
                passphrase,
            }, cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<WifiActionResponseDto>(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new WifiActionResponseDto
                {
                    Status = payload?.Status ?? "failed",
                    Message = payload?.Message ?? "Privileged host agent returned an error for Wi-Fi connect.",
                    ErrorCode = payload?.ErrorCode ?? "agent_connect_error",
                    ProgressPercent = payload?.ProgressPercent,
                    ChangedAtUtc = DateTime.UtcNow,
                };
            }

            if (payload is not null)
            {
                return payload;
            }
        }
        catch
        {
        }

        return new WifiActionResponseDto
        {
            Status = "unavailable",
            Message = "Privileged host agent is unreachable.",
            ErrorCode = "agent_unreachable",
            ProgressPercent = 0,
            ChangedAtUtc = DateTime.UtcNow,
        };
    }

    public async Task<WifiActionResponseDto> DisconnectWifiAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/agent/wifi/disconnect", new { }, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<WifiActionResponseDto>(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new WifiActionResponseDto
                {
                    Status = payload?.Status ?? "failed",
                    Message = payload?.Message ?? "Privileged host agent returned an error for Wi-Fi disconnect.",
                    ErrorCode = payload?.ErrorCode ?? "agent_disconnect_error",
                    ProgressPercent = payload?.ProgressPercent,
                    ChangedAtUtc = DateTime.UtcNow,
                };
            }

            if (payload is not null)
            {
                return payload;
            }
        }
        catch
        {
        }

        return new WifiActionResponseDto
        {
            Status = "unavailable",
            Message = "Privileged host agent is unreachable.",
            ErrorCode = "agent_unreachable",
            ProgressPercent = 0,
            ChangedAtUtc = DateTime.UtcNow,
        };
    }
}
