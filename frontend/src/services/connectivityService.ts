import { apiGet, apiPost } from "./api/client";

export interface NetworkInterfaceSummary {
  name: string;
  type: string;
  ipAddress: string;
  isWireless: boolean;
  isPrivateAddress: boolean;
}

export interface HostConnectivitySnapshot {
  hostname: string;
  accessMode: "LanCompatible" | "LocalOnly";
  serviceBind: string;
  allowedHosts: string;
  lanDeploymentEnabled: boolean;
  recommendedUrls: string[];
  activeInterfaces: NetworkInterfaceSummary[];
  warnings: string[];
  generatedAtUtc: string;
}

export async function getConnectivitySnapshot(): Promise<HostConnectivitySnapshot> {
  return apiGet<HostConnectivitySnapshot>("/admin/system/connectivity");
}

export interface WifiStatus {
  isConnected: boolean;
  ssid?: string | null;
  bssid?: string | null;
  signalQualityPercent?: number | null;
  interfaceName?: string | null;
  ipAddress?: string | null;
  message?: string | null;
  checkedAtUtc: string;
}

export interface WifiNetwork {
  ssid: string;
  signalQualityPercent: number;
  security: string;
  isConnected: boolean;
}

export interface WifiScanResult {
  networks: WifiNetwork[];
  message?: string | null;
  scannedAtUtc: string;
}

export interface WifiConnectRequest {
  ssid: string;
  passphrase: string;
  reauthToken: string;
}

export interface WifiDisconnectRequest {
  reauthToken: string;
}

export interface WifiActionResult {
  status: string;
  message: string;
  changedAtUtc: string;
}

export async function getWifiStatus(): Promise<WifiStatus> {
  return apiGet<WifiStatus>("/admin/system/wifi/status");
}

export async function scanWifiNetworks(): Promise<WifiScanResult> {
  return apiGet<WifiScanResult>("/admin/system/wifi/scan");
}

export async function connectWifi(request: WifiConnectRequest): Promise<WifiActionResult> {
  return apiPost<WifiActionResult, WifiConnectRequest>("/admin/system/wifi/connect", request);
}

export async function disconnectWifi(request: WifiDisconnectRequest): Promise<WifiActionResult> {
  return apiPost<WifiActionResult, WifiDisconnectRequest>("/admin/system/wifi/disconnect", request);
}
