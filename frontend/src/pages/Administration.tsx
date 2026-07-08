import {
  Alert,
  Box,
  Button,
  Divider,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Switch,
  TextField,
  Typography,
  CircularProgress,
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";

import {
  addLine,
  getAllLines,
  updateLine,
} from "../services/dashboardService";
import {
  testPlcConnection,
  type PlcConnectionResult,
} from "../services/plcConnectionService";
import { ApiRequestError } from "../services/api/client";
import { LineStatus } from "../types/LineStatus";
import type { PlcManufacturer, ProductionLine } from "../types/ProductionLine";

type LineConfigForm = {
  lineNumber: number;
  lineName: string;
  product: string;
  plcIp: string;
  manufacturer: PlcManufacturer;
  isActive: boolean;
};

const emptyLineForm: LineConfigForm = {
  lineNumber: 1,
  lineName: "",
  product: "000-000-00-0",
  plcIp: "",
  manufacturer: "AllenBradley",
  isActive: true,
};

const productSerialPattern = /^\d{3}-\d{3}-\d{2}-\d{1}$/;

function isValidIpv4Address(value: string): boolean {
  const ipv4Pattern =
    /^(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}$/;

  return ipv4Pattern.test(value.trim());
}

function formatLineTitle(line: ProductionLine) {
  return `Line ${line.lineNumber} - ${line.lineName}`;
}

function formatConnectionTaskTrace(result: PlcConnectionResult): string {
  const lines = [
    `Task 1 complete: prepared ${result.driver} PLC connection test`,
    `Task 2 complete: sent connection request for ${result.ipAddress}`,
    `Task 3 complete: received PLC response`,
    `Task 4 result: isConnected = ${String(result.isConnected)}`,
    `Task 5 detail: ${result.message}`,
  ];

  if (result.controllerName) {
    lines.push(`Task 6 detail: controller = ${result.controllerName}`);
  }

  if (typeof result.responseTimeMs === "number") {
    lines.push(`Task 7 detail: responseTimeMs = ${result.responseTimeMs}`);
  }

  return lines.join("\n");
}

function formatRequestError(requestError: ApiRequestError): string {
  const lines = [requestError.message];

  if (requestError.responseBody) {
    lines.push(`Response body: ${requestError.responseBody}`);
  }

  return lines.join("\n");
}

export default function Administration() {
  const [lines, setLines] = useState<ProductionLine[]>([]);
  const [selectedLineId, setSelectedLineId] = useState<number | "new">("new");
  const [form, setForm] = useState<LineConfigForm>(emptyLineForm);
  const [error, setError] = useState<string>("");
  const [success, setSuccess] = useState<string>("");
  const [testingConnection, setTestingConnection] = useState(false);
  const [connectionResult, setConnectionResult] = useState<PlcConnectionResult | null>(null);

  const activeLines = useMemo(
    () => lines.filter((line) => line.isActive),
    [lines]
  );

  const selectedLine = useMemo(() => {
    if (selectedLineId === "new") {
      return null;
    }

    return lines.find((line) => line.id === selectedLineId) ?? null;
  }, [lines, selectedLineId]);

  useEffect(() => {
    void loadLines();
  }, []);

  async function loadLines() {
    const fetchedLines = await getAllLines();
    setLines(fetchedLines);
  }

  function handleSelectLine(value: number | "new") {
    setSelectedLineId(value);
    setError("");
    setSuccess("");

    if (value === "new") {
      setForm(emptyLineForm);
      return;
    }

    const selected = lines.find((line) => line.id === value);

    if (selected) {
      const lineForm: LineConfigForm = {
        lineNumber: selected.lineNumber,
        lineName: selected.lineName,
        product: selected.product,
        plcIp: selected.plcIp,
        manufacturer: selected.manufacturer,
        isActive: selected.isActive,
      };

      setForm(lineForm);
    }
  }

  function updateForm<K extends keyof LineConfigForm>(
    key: K,
    value: LineConfigForm[K]
  ) {
    if (key === "manufacturer" || key === "plcIp") {
      setConnectionResult(null);
      setSuccess("");
    }

    setForm((previous) => ({
      ...previous,
      [key]: value,
    }));
  }

  function validateForm(): string | null {
    const trimmedProduct = form.product.trim();
    const trimmedLineName = form.lineName.trim();
    const trimmedIp = form.plcIp.trim();

    if (!trimmedLineName) {
      return "Line name is required.";
    }

    if (!form.product.trim()) {
      return "Product is required.";
    }

    if (!productSerialPattern.test(trimmedProduct)) {
      return "Product serial must match xxx-xxx-xx-x using digits.";
    }

    if (!trimmedIp) {
      return "PLC IP is required.";
    }

    if (!isValidIpv4Address(trimmedIp)) {
      return "PLC IP must be a valid IPv4 address (example: 192.168.1.105).";
    }

    if (form.lineNumber <= 0) {
      return "Line number must be greater than 0.";
    }

    const duplicateLineNumber = lines.some((line) => {
      if (selectedLineId !== "new" && line.id === selectedLineId) {
        return false;
      }

      return line.lineNumber === form.lineNumber;
    });

    if (duplicateLineNumber) {
      return `Line number ${form.lineNumber} is already in use.`;
    }

    const duplicateIp = lines.some((line) => {
      if (selectedLineId !== "new" && line.id === selectedLineId) {
        return false;
      }

      return line.plcIp.trim() === trimmedIp;
    });

    if (duplicateIp) {
      return `PLC IP ${trimmedIp} is already assigned to another line.`;
    }

    if (!trimmedProduct) {
      return "Product is required.";
    }

    return null;
  }

  async function handleSave() {
    setError("");
    setSuccess("");

    const validationError = validateForm();

    if (validationError) {
      setError(validationError);
      return;
    }

    if (selectedLineId === "new") {
      await addLine({
        ...form,
        lineName: form.lineName.trim(),
        startDateTime: new Date().toISOString(),
        status: LineStatus.Offline,
        timeInStatus: "00:00:00",
        controlMode: "Auto",
        percentAutoMode: 100,
        autoVariance: 0,
        percentManualMode: 0,
        manualVariance: 0,
        totalVariance: 0,
        totalLength: 0,
        runtime: "00:00:00",
        product: form.product.trim(),
        plcIp: form.plcIp.trim(),
      });
      setSuccess("Line added successfully.");
    } else {
      await updateLine(selectedLineId, {
        lineNumber: form.lineNumber,
        lineName: form.lineName.trim(),
        product: form.product.trim(),
        manufacturer: form.manufacturer,
        plcIp: form.plcIp.trim(),
        isActive: form.isActive,
      });
      setSuccess("Line updated successfully.");
    }

    const refreshed = await getAllLines();
    setLines(refreshed);

    if (selectedLineId === "new") {
      const newest = refreshed[refreshed.length - 1];

      if (newest) {
        setSelectedLineId(newest.id);
      }
    }
  }

  async function handleTestConnection() {
    setError("");
    setSuccess("");
    setTestingConnection(true);

    try {
      const result = await testPlcConnection({
        driver: form.manufacturer,
        ipAddress: form.plcIp.trim(),
      });

      setConnectionResult(result);

      if (result.isConnected) {
        setSuccess("PLC connection verified successfully.");
      } else {
        setError(formatConnectionTaskTrace(result));
      }
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? formatRequestError(requestError)
          : requestError instanceof Error
            ? requestError.message
            : "PLC connection test failed.";

      setConnectionResult({
        isConnected: false,
        driver: form.manufacturer,
        ipAddress: form.plcIp.trim(),
        message,
      });
      setError(message);
    } finally {
      setTestingConnection(false);
    }
  }

  const canTestConnection = isValidIpv4Address(form.plcIp) && !testingConnection;
  const isNewLine = selectedLineId === "new";
  const canSaveLine = !isNewLine || connectionResult?.isConnected === true;

  return (
    <Stack spacing={3}>
      <Typography variant="h4" sx={{ fontWeight: 700 }}>
        Administration
      </Typography>

      <Paper sx={{ p: 3 }}>
        <Stack spacing={1.5}>
          <Typography variant="h6">Active Lines</Typography>
          <Typography color="text.secondary">
            {activeLines.length} active line{activeLines.length === 1 ? "" : "s"}
          </Typography>

          <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
            {activeLines.map((line) => (
              <Button
                key={line.id}
                variant={selectedLineId === line.id ? "contained" : "outlined"}
                onClick={() => handleSelectLine(line.id)}
              >
                {`Line ${line.lineNumber} - ${line.lineName}`}
              </Button>
            ))}
          </Box>
        </Stack>
      </Paper>

      <Paper sx={{ p: 3 }}>
        <Stack spacing={2.5}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            justifyContent="space-between"
            alignItems={{ xs: "flex-start", sm: "center" }}
            spacing={2}
          >
            <Typography variant="h6">Line Configuration</Typography>

            <FormControl size="small" sx={{ minWidth: 220 }}>
              <InputLabel id="line-select-label">Record</InputLabel>
              <Select
                labelId="line-select-label"
                value={selectedLineId}
                label="Record"
                onChange={(event) =>
                  handleSelectLine(event.target.value as number | "new")
                }
              >
                <MenuItem value="new">Add New Line</MenuItem>
                {lines.map((line) => (
                  <MenuItem key={line.id} value={line.id}>
                    {formatLineTitle(line)}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>

          <Divider />

          {error ? (
            <Alert severity="error">
              <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                {error}
              </Typography>
            </Alert>
          ) : null}
          {success ? (
            <Alert severity="success">
              <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                {success}
              </Typography>
            </Alert>
          ) : null}

          <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
            <TextField
              label="Line Number"
              type="number"
              value={form.lineNumber}
              onChange={(event) =>
                updateForm("lineNumber", Number(event.target.value))
              }
              fullWidth
            />

            <TextField
              label="Line Name"
              value={form.lineName}
              onChange={(event) => updateForm("lineName", event.target.value)}
              fullWidth
              placeholder="Main Extruder"
            />

            <FormControl fullWidth>
              <InputLabel id="manufacturer-label">PLC Driver</InputLabel>
              <Select
                labelId="manufacturer-label"
                label="PLC Driver"
                value={form.manufacturer}
                onChange={(event) =>
                  updateForm("manufacturer", event.target.value as PlcManufacturer)
                }
              >
                <MenuItem value="AllenBradley">AllenBradley</MenuItem>
                <MenuItem value="Siemens">Siemens</MenuItem>
              </Select>
            </FormControl>
          </Stack>

          <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
            <TextField
              label="Product Serial"
              value={form.product}
              onChange={(event) => updateForm("product", event.target.value)}
              fullWidth
              placeholder="123-456-78-9"
            />

            <TextField
              label="PLC IP Address"
              value={form.plcIp}
              onChange={(event) => updateForm("plcIp", event.target.value)}
              fullWidth
              placeholder="192.168.1.105"
            />
          </Stack>

          <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
            <TextField
              label="Status (PLC)"
              value={selectedLine?.status ?? "Offline"}
              fullWidth
              disabled
            />

            <TextField
              label="Control Mode (PLC)"
              value={selectedLine?.controlMode ?? "Auto"}
              fullWidth
              disabled
            />
          </Stack>

          <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
            <TextField
              label="Total Length (ft) (PLC)"
              value={selectedLine ? selectedLine.totalLength.toLocaleString() : "0"}
              fullWidth
              disabled
            />

            <TextField
              label="Runtime (PLC)"
              value={selectedLine?.runtime ?? "00:00:00"}
              fullWidth
              disabled
            />
          </Stack>

          <Alert severity="info">
            Status, control mode, total length, and runtime are PLC-driven values and cannot be edited here.
            Administration is used for network and line configuration.
          </Alert>

          <Paper
            variant="outlined"
            sx={{
              p: 2,
              borderColor: connectionResult?.isConnected ? "success.main" : "divider",
              backgroundColor: connectionResult?.isConnected ? "success.50" : "background.paper",
            }}
          >
            <Stack spacing={2}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                PLC Connection Test
              </Typography>

              <Typography color="text.secondary">
                Test the selected driver against the entered PLC IP before saving the line.
              </Typography>

              {connectionResult ? (
                <Alert severity={connectionResult.isConnected ? "success" : "error"}>
                  <Stack spacing={0.5}>
                    <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                      {connectionResult.message}
                    </Typography>
                    <Typography variant="body2">
                      Is Connected: {String(connectionResult.isConnected)}
                    </Typography>
                    <Typography variant="body2">
                      Driver: {connectionResult.driver} | IP: {connectionResult.ipAddress}
                    </Typography>
                    {connectionResult.controllerName ? (
                      <Typography variant="body2">
                        Controller: {connectionResult.controllerName}
                      </Typography>
                    ) : null}
                    {connectionResult.firmware ? (
                      <Typography variant="body2">Firmware: {connectionResult.firmware}</Typography>
                    ) : null}
                    {typeof connectionResult.responseTimeMs === "number" ? (
                      <Typography variant="body2">
                        Response time: {connectionResult.responseTimeMs} ms
                      </Typography>
                    ) : null}
                  </Stack>
                </Alert>
              ) : null}

              <Stack direction={{ xs: "column", sm: "row" }} spacing={2} justifyContent="flex-end">
                <Button
                  variant="outlined"
                  onClick={handleTestConnection}
                  disabled={!canTestConnection}
                >
                  {testingConnection ? (
                    <Stack direction="row" spacing={1} alignItems="center">
                      <CircularProgress size={16} color="inherit" />
                      <span>Testing...</span>
                    </Stack>
                  ) : (
                    "Test Connection"
                  )}
                </Button>
              </Stack>
            </Stack>
          </Paper>

          <FormControlLabel
            control={
              <Switch
                checked={form.isActive}
                onChange={(event) => updateForm("isActive", event.target.checked)}
              />
            }
            label="Active Line"
          />

          <Stack direction="row" justifyContent="flex-end">
            <Button variant="contained" onClick={handleSave} disabled={!canSaveLine}>
              {selectedLineId === "new" ? "Add Line" : "Save Changes"}
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </Stack>
  );
}
