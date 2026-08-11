import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  FormControl,
  FormControlLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Switch,
  TextField,
  Typography,
} from "@mui/material";
import { getContrastRatio } from "@mui/material/styles";
import LaunchIcon from "@mui/icons-material/Launch";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";

import { ApiRequestError } from "../services/api/client";
import {
  connectWifi,
  disconnectWifi,
  getConnectivitySnapshot,
  getWifiStatus,
  scanWifiNetworks,
  type HostConnectivitySnapshot,
  type WifiActionResult,
  type WifiScanResult,
  type WifiStatus,
} from "../services/connectivityService";
import { reauthenticateAdmin } from "../services/otaService";
import {
  type AppearanceSettings,
  type TextSize,
} from "../context/ThemeContext";
import { useThemeMode } from "../context/useThemeMode";

const textSizeOptions: Array<{ value: TextSize; label: string }> = [
  { value: "small", label: "Small" },
  { value: "medium", label: "Medium" },
  { value: "large", label: "Large" },
];

function getReadableTextColor(background: string) {
  const whiteContrast = getContrastRatio(background, "#FFFFFF");
  const darkContrast = getContrastRatio(background, "#0F172A");

  return whiteContrast >= darkContrast ? "#FFFFFF" : "#0F172A";
}

const themePresets: Array<{
  id: string;
  label: string;
  description: string;
  colors: Pick<AppearanceSettings, "headerColor" | "backgroundColor" | "tableColor">;
}> = [
  {
    id: "classic",
    label: "Classic Plant",
    description: "Balanced blue with bright table contrast",
    colors: {
      headerColor: "#1565C0",
      backgroundColor: "#F4F6F8",
      tableColor: "#E3F2FD",
    },
  },
  {
    id: "spec-ops",
    label: "Spec Ops",
    description: "Low-glare dark tactical display",
    colors: {
      headerColor: "#0B1F35",
      backgroundColor: "#121820",
      tableColor: "#223247",
    },
  },
  {
    id: "high-visibility",
    label: "High Visibility",
    description: "Strong contrast for long-distance viewing",
    colors: {
      headerColor: "#A61B1B",
      backgroundColor: "#F5F0E8",
      tableColor: "#FFE8C2",
    },
  },
  {
    id: "industrial-green",
    label: "Industrial Green",
    description: "Factory-style green and steel tones",
    colors: {
      headerColor: "#1D5C45",
      backgroundColor: "#E9EFEC",
      tableColor: "#D5E4DD",
    },
  },
];

function formatRequestError(requestError: ApiRequestError): string {
  const lines = [requestError.message];

  if (requestError.responseBody) {
    lines.push(`Response body: ${requestError.responseBody}`);
  }

  return lines.join("\n");
}

export default function Settings() {
  const navigate = useNavigate();
  const {
    mode,
    isDarkMode,
    setMode,
    toggleMode,
    appearance,
    updateAppearance,
    resetAppearance,
  } = useThemeMode();
  const [connectivityLoading, setConnectivityLoading] = useState(false);
  const [connectivityError, setConnectivityError] = useState("");
  const [connectivitySnapshot, setConnectivitySnapshot] = useState<HostConnectivitySnapshot | null>(null);
  const [wifiStatus, setWifiStatus] = useState<WifiStatus | null>(null);
  const [wifiScanResult, setWifiScanResult] = useState<WifiScanResult | null>(null);
  const [wifiActionResult, setWifiActionResult] = useState<WifiActionResult | null>(null);
  const [wifiSsidInput, setWifiSsidInput] = useState("");
  const [wifiPassphraseInput, setWifiPassphraseInput] = useState("");
  const [wifiLoading, setWifiLoading] = useState(false);
  const [wifiStatusMessage, setWifiStatusMessage] = useState("");
  const [wifiReauthAction, setWifiReauthAction] = useState<"connect" | "disconnect">("connect");
  const [wifiReauthDialogOpen, setWifiReauthDialogOpen] = useState(false);
  const [wifiReauthPassword, setWifiReauthPassword] = useState("");
  const [wifiReauthSubmitting, setWifiReauthSubmitting] = useState(false);

  useEffect(() => {
    void loadConnectivitySnapshot();
    void loadWifiStatus();
    void loadWifiScan();
  }, []);

  async function loadConnectivitySnapshot() {
    setConnectivityLoading(true);
    setConnectivityError("");

    try {
      const snapshot = await getConnectivitySnapshot();
      setConnectivitySnapshot(snapshot);
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Connectivity diagnostics request failed.";
      setConnectivityError(message);
      setConnectivitySnapshot(null);
    } finally {
      setConnectivityLoading(false);
    }
  }

  async function copyConnectivityUrl(url: string) {
    if (!navigator?.clipboard) {
      setConnectivityError("Clipboard access is not available in this browser.");
      return;
    }

    try {
      await navigator.clipboard.writeText(url);
      setWifiStatusMessage(`Copied URL: ${url}`);
    } catch {
      setConnectivityError("Unable to copy URL to clipboard.");
    }
  }

  async function loadWifiStatus() {
    setWifiLoading(true);
    setWifiStatusMessage("");

    try {
      const status = await getWifiStatus();
      setWifiStatus(status);
      if (status.message) {
        setWifiStatusMessage(status.message);
      }
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Wi-Fi status request failed.";
      setWifiStatusMessage(message);
      setWifiStatus(null);
    } finally {
      setWifiLoading(false);
    }
  }

  async function loadWifiScan() {
    setWifiLoading(true);
    setWifiStatusMessage("");

    try {
      const result = await scanWifiNetworks();
      setWifiScanResult(result);
      if (result.networks.length > 0 && !wifiSsidInput.trim()) {
        const connected = result.networks.find((network) => network.isConnected);
        setWifiSsidInput((connected ?? result.networks[0]).ssid);
      }
      if (result.message) {
        setWifiStatusMessage(result.message);
      }
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Wi-Fi scan request failed.";
      setWifiStatusMessage(message);
      setWifiScanResult(null);
    } finally {
      setWifiLoading(false);
    }
  }

  function handleConnectWifi() {
    setWifiStatusMessage("");
    if (!wifiSsidInput.trim()) {
      setWifiStatusMessage("Wi-Fi SSID is required.");
      return;
    }

    setWifiReauthAction("connect");
    setWifiReauthDialogOpen(true);
  }

  function handleDisconnectWifi() {
    setWifiStatusMessage("");
    setWifiReauthAction("disconnect");
    setWifiReauthDialogOpen(true);
  }

  async function confirmWifiReauth() {
    setWifiReauthSubmitting(true);
    setWifiStatusMessage("");

    try {
      const reauth = await reauthenticateAdmin({
        password: wifiReauthPassword,
        scope: "wifi-manage",
      });

      setWifiLoading(true);
      if (wifiReauthAction === "connect") {
        const result = await connectWifi({
          ssid: wifiSsidInput.trim(),
          passphrase: wifiPassphraseInput,
          reauthToken: reauth.token,
        });
        setWifiActionResult(result);
        setWifiStatusMessage(result.message);
      } else {
        const result = await disconnectWifi({
          reauthToken: reauth.token,
        });
        setWifiActionResult(result);
        setWifiStatusMessage(result.message);
      }

      await loadWifiStatus();
      await loadWifiScan();
      setWifiReauthPassword("");
      setWifiReauthDialogOpen(false);
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "Admin re-authentication failed.";
      setWifiStatusMessage(message);
    } finally {
      setWifiLoading(false);
      setWifiReauthSubmitting(false);
    }
  }

  return (
      <Stack spacing={2.5}>
        <Typography variant="h4" sx={{ fontWeight: 700 }}>
          Display Settings
        </Typography>

        <Typography color="text.secondary">
          These preferences are stored in this browser for the currently signed-in user and are not shared to other devices.
        </Typography>

        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Typography variant="h6">Color Mode</Typography>

            <FormControlLabel
              control={<Switch checked={isDarkMode} onChange={toggleMode} />}
              label={isDarkMode ? "Dark Mode Enabled" : "Light Mode Enabled"}
            />

            <FormControl size="small" sx={{ maxWidth: 260 }}>
              <Select
                value={mode}
                onChange={(event) =>
                  setMode(event.target.value as "light" | "dark")
                }
              >
                <MenuItem value="light">Light</MenuItem>
                <MenuItem value="dark">Dark</MenuItem>
              </Select>
            </FormControl>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Typography variant="h6">Branding and Theme Presets</Typography>

            <TextField
              label="Monitor Name"
              size="small"
              value={appearance.monitorName}
              onChange={(event) =>
                updateAppearance({ monitorName: event.target.value })
              }
              helperText="Used in page titles, for example: Plant Monitor Dashboard"
              sx={{ maxWidth: 360 }}
            />

            <Typography color="text.secondary">
              Choose a preset for instant color changes without the color-picker lag.
            </Typography>

            <Stack spacing={1.2}>
              {themePresets.map((preset) => {
                const isActive =
                  appearance.headerColor.toLowerCase() === preset.colors.headerColor.toLowerCase() &&
                  appearance.backgroundColor.toLowerCase() === preset.colors.backgroundColor.toLowerCase() &&
                  appearance.tableColor.toLowerCase() === preset.colors.tableColor.toLowerCase();
                const headerChipText = getReadableTextColor(preset.colors.headerColor);
                const backgroundChipText = getReadableTextColor(preset.colors.backgroundColor);
                const tableChipText = getReadableTextColor(preset.colors.tableColor);

                return (
                  <Button
                    key={preset.id}
                    variant={isActive ? "contained" : "outlined"}
                    onClick={() => updateAppearance(preset.colors)}
                    sx={{
                      justifyContent: "space-between",
                      textTransform: "none",
                      py: 1,
                      px: 1.4,
                      ...(isActive && {
                        color: (theme) => theme.palette.primary.contrastText,
                      }),
                    }}
                  >
                    <Stack alignItems="flex-start" spacing={0.4}>
                      <Typography sx={{ fontWeight: 700 }}>{preset.label}</Typography>
                      <Typography variant="body2" color="text.secondary">
                        {preset.description}
                      </Typography>
                    </Stack>

                    <Stack direction="row" spacing={0.6}>
                      <Chip
                        label="H"
                        size="small"
                        sx={{
                          bgcolor: preset.colors.headerColor,
                          color: headerChipText,
                          fontWeight: 700,
                        }}
                      />
                      <Chip
                        label="B"
                        size="small"
                        sx={{
                          bgcolor: preset.colors.backgroundColor,
                          color: backgroundChipText,
                          fontWeight: 700,
                        }}
                      />
                      <Chip
                        label="T"
                        size="small"
                        sx={{
                          bgcolor: preset.colors.tableColor,
                          color: tableChipText,
                          fontWeight: 700,
                        }}
                      />
                    </Stack>
                  </Button>
                );
              })}
            </Stack>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Typography variant="h6">Text Readability</Typography>

            <FormControl size="small" sx={{ maxWidth: 220 }}>
              <Select
                value={appearance.textSize}
                onChange={(event) =>
                  updateAppearance({ textSize: event.target.value as TextSize })
                }
              >
                {textSizeOptions.map((option) => (
                  <MenuItem key={option.value} value={option.value}>
                    {option.label}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <FormControlLabel
              control={
                <Switch
                  checked={appearance.boldText}
                  onChange={(event) =>
                    updateAppearance({ boldText: event.target.checked })
                  }
                />
              }
              label="Use bolder text across the app"
            />
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Stack
              direction={{ xs: "column", sm: "row" }}
              justifyContent="space-between"
              alignItems={{ xs: "flex-start", sm: "center" }}
              spacing={1}
            >
              <Box>
                <Typography variant="h6">Network Settings</Typography>
                <Typography color="text.secondary">
                  Manage factory Wi-Fi dashboard reachability and host-side Wi-Fi actions from one place.
                </Typography>
              </Box>

              <Button
                variant="outlined"
                onClick={() => {
                  void loadConnectivitySnapshot();
                }}
                disabled={connectivityLoading}
              >
                {connectivityLoading ? "Refreshing..." : "Refresh"}
              </Button>
            </Stack>

            {connectivityError ? (
              <Alert severity="warning">
                <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                  {connectivityError}
                </Typography>
              </Alert>
            ) : null}

            {connectivitySnapshot ? (
              <Stack spacing={1.5}>
                <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
                  <Alert severity={connectivitySnapshot.accessMode === "LanCompatible" ? "success" : "error"} sx={{ flex: 1 }}>
                    <Typography variant="body2">
                      Access Mode: {connectivitySnapshot.accessMode}
                    </Typography>
                    <Typography variant="body2">
                      Hostname: {connectivitySnapshot.hostname}
                    </Typography>
                  </Alert>

                  <Alert severity="info" sx={{ flex: 1 }}>
                    <Typography variant="body2">Service Bind: {connectivitySnapshot.serviceBind}</Typography>
                    <Typography variant="body2">Allowed Hosts: {connectivitySnapshot.allowedHosts}</Typography>
                  </Alert>
                </Stack>

                {connectivitySnapshot.warnings.length > 0 ? (
                  <Alert severity="warning">
                    <Typography variant="body2" sx={{ mb: 0.5, fontWeight: 700 }}>
                      Connectivity Warnings
                    </Typography>
                    {connectivitySnapshot.warnings.map((warning) => (
                      <Typography key={warning} variant="body2">
                        - {warning}
                      </Typography>
                    ))}
                  </Alert>
                ) : null}

                <Paper variant="outlined" sx={{ p: 2 }}>
                  <Stack spacing={1}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      Recommended Dashboard URLs
                    </Typography>
                    {connectivitySnapshot.recommendedUrls.map((url) => (
                      <Stack key={url} direction={{ xs: "column", sm: "row" }} spacing={1} alignItems={{ xs: "stretch", sm: "center" }}>
                        <TextField
                          value={url}
                          size="small"
                          fullWidth
                          InputProps={{ readOnly: true }}
                        />
                        <Button variant="outlined" onClick={() => { void copyConnectivityUrl(url); }}>
                          Copy
                        </Button>
                      </Stack>
                    ))}
                  </Stack>
                </Paper>

                <Paper variant="outlined" sx={{ p: 2 }}>
                  <Stack spacing={1}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      Active Host Interfaces
                    </Typography>
                    {connectivitySnapshot.activeInterfaces.length === 0 ? (
                      <Typography color="text.secondary">
                        No active non-loopback IPv4 interfaces were found.
                      </Typography>
                    ) : (
                      connectivitySnapshot.activeInterfaces.map((networkRow) => (
                        <Typography key={`${networkRow.name}-${networkRow.ipAddress}`} variant="body2">
                          {networkRow.name} ({networkRow.type}) - {networkRow.ipAddress}{networkRow.isWireless ? " [wireless]" : ""}
                        </Typography>
                      ))
                    )}
                  </Stack>
                </Paper>

                <Paper variant="outlined" sx={{ p: 2 }}>
                  <Stack spacing={1.5}>
                    <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" alignItems={{ xs: "flex-start", sm: "center" }} spacing={1}>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                        Wi-Fi Network Management (Privileged Agent)
                      </Typography>
                      <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
                        <Button
                          variant="outlined"
                          onClick={() => {
                            void loadWifiStatus();
                          }}
                          disabled={wifiLoading}
                        >
                          Refresh Status
                        </Button>
                        <Button
                          variant="outlined"
                          onClick={() => {
                            void loadWifiScan();
                          }}
                          disabled={wifiLoading}
                        >
                          Scan SSIDs
                        </Button>
                      </Stack>
                    </Stack>

                    {wifiStatus ? (
                      <Alert severity={wifiStatus.isConnected ? "success" : "info"}>
                        <Typography variant="body2">
                          Connected: {String(wifiStatus.isConnected)}
                        </Typography>
                        <Typography variant="body2">
                          SSID: {wifiStatus.ssid ?? "(none)"}
                        </Typography>
                        <Typography variant="body2">
                          Signal: {typeof wifiStatus.signalQualityPercent === "number" ? `${wifiStatus.signalQualityPercent}%` : "n/a"}
                        </Typography>
                      </Alert>
                    ) : null}

                    <TextField
                      label="Wi-Fi SSID"
                      value={wifiSsidInput}
                      onChange={(event) => setWifiSsidInput(event.target.value)}
                      fullWidth
                      placeholder="Factory-Wifi-A"
                    />

                    <TextField
                      label="Wi-Fi Passphrase"
                      type="password"
                      value={wifiPassphraseInput}
                      onChange={(event) => setWifiPassphraseInput(event.target.value)}
                      fullWidth
                      placeholder="Optional for open/test networks"
                    />

                    <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
                      <Button
                        variant="contained"
                        color="warning"
                        onClick={handleConnectWifi}
                        disabled={wifiLoading || wifiReauthSubmitting || wifiSsidInput.trim().length === 0}
                      >
                        Connect Wi-Fi (Re-auth Required)
                      </Button>
                      <Button
                        variant="outlined"
                        onClick={handleDisconnectWifi}
                        disabled={wifiLoading || wifiReauthSubmitting}
                      >
                        Disconnect Wi-Fi (Re-auth Required)
                      </Button>
                    </Stack>

                    {wifiScanResult ? (
                      <Paper variant="outlined" sx={{ p: 1.5 }}>
                        <Stack spacing={0.5}>
                          <Typography variant="body2" sx={{ fontWeight: 700 }}>Available SSIDs</Typography>
                          {wifiScanResult.networks.length === 0 ? (
                            <Typography variant="body2" color="text.secondary">No networks returned by privileged agent.</Typography>
                          ) : (
                            wifiScanResult.networks.map((network) => (
                              <Typography key={network.ssid} variant="body2">
                                {network.ssid} - {network.signalQualityPercent}% - {network.security}{network.isConnected ? " [connected]" : ""}
                              </Typography>
                            ))
                          )}
                        </Stack>
                      </Paper>
                    ) : null}

                    {wifiActionResult ? (
                      <Typography variant="body2" color="text.secondary">
                        Last action: {wifiActionResult.status} at {new Date(wifiActionResult.changedAtUtc).toLocaleString()}
                      </Typography>
                    ) : null}

                    {wifiStatusMessage ? (
                      <Alert severity="info">
                        <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>{wifiStatusMessage}</Typography>
                      </Alert>
                    ) : null}
                  </Stack>
                </Paper>
              </Stack>
            ) : null}
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Typography variant="h6">Factory Floor Board</Typography>
            <Typography color="text.secondary">
              Open the airplane departure-style status board for large-screen floor viewing.
            </Typography>

            <Button
              variant="contained"
              startIcon={<LaunchIcon />}
              onClick={() => navigate("/status-board")}
              sx={{ alignSelf: "flex-start" }}
            >
              Open Status Board
            </Button>
          </Stack>
        </Paper>

        <Stack direction="row" spacing={1.5}>
          <Button variant="outlined" onClick={resetAppearance}>
            Reset Appearance
          </Button>
        </Stack>

        <Dialog
          open={wifiReauthDialogOpen}
          onClose={() => {
            if (wifiReauthSubmitting || wifiLoading) {
              return;
            }

            setWifiReauthDialogOpen(false);
            setWifiReauthPassword("");
          }}
        >
          <DialogTitle>Confirm Admin Password</DialogTitle>
          <DialogContent>
            <Stack spacing={1.5} sx={{ pt: 0.5, minWidth: { xs: 260, sm: 420 } }}>
              <DialogContentText>
                Re-enter your admin password to authorize Wi-Fi network changes through the privileged host agent.
              </DialogContentText>
              <TextField
                label="Admin Password"
                type="password"
                value={wifiReauthPassword}
                onChange={(event) => setWifiReauthPassword(event.target.value)}
                autoFocus
                fullWidth
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button
              onClick={() => {
                setWifiReauthDialogOpen(false);
                setWifiReauthPassword("");
              }}
              disabled={wifiReauthSubmitting || wifiLoading}
            >
              Cancel
            </Button>
            <Button
              variant="contained"
              onClick={() => {
                void confirmWifiReauth();
              }}
              disabled={wifiReauthSubmitting || wifiLoading || wifiReauthPassword.trim().length === 0}
            >
              {wifiReauthSubmitting || wifiLoading ? "Authorizing..." : "Authorize"}
            </Button>
          </DialogActions>
        </Dialog>
      </Stack>
  );
}