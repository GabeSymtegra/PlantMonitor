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
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";

import {
  addLine,
  getAllLines,
  updateLine,
} from "../services/dashboardService";
import { LineStatus } from "../types/LineStatus";
import type { PlcManufacturer, ProductionLine } from "../types/ProductionLine";

type LineConfigForm = {
  lineNumber: number;
  product: string;
  plcIp: string;
  manufacturer: PlcManufacturer;
  isActive: boolean;
};

const emptyLineForm: LineConfigForm = {
  lineNumber: 1,
  product: "",
  plcIp: "",
  manufacturer: "AB",
  isActive: true,
};

function isValidIpv4Address(value: string): boolean {
  const ipv4Pattern =
    /^(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}$/;

  return ipv4Pattern.test(value.trim());
}

function formatLineTitle(line: ProductionLine) {
  return `Line ${line.lineNumber} - ${line.product || "Unassigned"}`;
}

export default function Administration() {
  const [lines, setLines] = useState<ProductionLine[]>([]);
  const [selectedLineId, setSelectedLineId] = useState<number | "new">("new");
  const [form, setForm] = useState<LineConfigForm>(emptyLineForm);
  const [error, setError] = useState<string>("");
  const [success, setSuccess] = useState<string>("");

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
    setForm((previous) => ({
      ...previous,
      [key]: value,
    }));
  }

  function validateForm(): string | null {
    const trimmedProduct = form.product.trim();
    const trimmedIp = form.plcIp.trim();

    if (!form.product.trim()) {
      return "Product is required.";
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
                {formatLineTitle(line)}
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

          {error ? <Alert severity="error">{error}</Alert> : null}
          {success ? <Alert severity="success">{success}</Alert> : null}

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

            <FormControl fullWidth>
              <InputLabel id="manufacturer-label">Manufacturer</InputLabel>
              <Select
                labelId="manufacturer-label"
                label="Manufacturer"
                value={form.manufacturer}
                onChange={(event) =>
                  updateForm("manufacturer", event.target.value as PlcManufacturer)
                }
              >
                <MenuItem value="AB">AB</MenuItem>
                <MenuItem value="Siemens">Siemens</MenuItem>
              </Select>
            </FormControl>
          </Stack>

          <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
            <TextField
              label="Product"
              value={form.product}
              onChange={(event) => updateForm("product", event.target.value)}
              fullWidth
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
            <Button variant="contained" onClick={handleSave}>
              {selectedLineId === "new" ? "Add Line" : "Save Changes"}
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </Stack>
  );
}
