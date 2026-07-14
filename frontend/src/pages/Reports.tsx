import {
  Alert,
  Box,
  Button,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";
import type { SelectChangeEvent } from "@mui/material";

import { useDashboard } from "../context/useDashboard";
import type {
  CompletedRunReportRow,
  RuntimeEventReportRow,
} from "../models/Reports";
import {
  getCompletedRunReports,
  getRuntimeEventReports,
} from "../services/reportsService";

function formatDateTime(isoUtc: string): string {
  return new Date(isoUtc).toLocaleString();
}

function formatDuration(seconds: number): string {
  const safeSeconds = Math.max(0, Math.floor(seconds));
  const hours = Math.floor(safeSeconds / 3600)
    .toString()
    .padStart(2, "0");
  const minutes = Math.floor((safeSeconds % 3600) / 60)
    .toString()
    .padStart(2, "0");
  const secs = (safeSeconds % 60).toString().padStart(2, "0");
  return `${hours}:${minutes}:${secs}`;
}

function toDateRangeStart(value: string): Date | null {
  if (!value) {
    return null;
  }

  return new Date(`${value}T00:00:00`);
}

function toDateRangeEnd(value: string): Date | null {
  if (!value) {
    return null;
  }

  return new Date(`${value}T23:59:59.999`);
}

function escapeCsvCell(value: string): string {
  const escaped = value.replace(/"/g, '""');
  return /[",\n]/.test(escaped) ? `"${escaped}"` : escaped;
}

function downloadCsv(filename: string, headers: string[], rows: string[][]): void {
  const content = [headers, ...rows]
    .map((row) => row.map(escapeCsvCell).join(","))
    .join("\n");

  const blob = new Blob([content], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

export default function Reports() {
  const { dashboard } = useDashboard();
  const [lineFilter, setLineFilter] = useState<string>("all");
  const [startDate, setStartDate] = useState<string>("");
  const [endDate, setEndDate] = useState<string>("");
  const [runs, setRuns] = useState<CompletedRunReportRow[]>([]);
  const [events, setEvents] = useState<RuntimeEventReportRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const selectedLineId = lineFilter === "all" ? undefined : Number(lineFilter);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const [runRows, eventRows] = await Promise.all([
          getCompletedRunReports(selectedLineId),
          getRuntimeEventReports(selectedLineId),
        ]);

        if (cancelled) {
          return;
        }

        setRuns(runRows);
        setEvents(eventRows);
      } catch (loadError) {
        if (cancelled) {
          return;
        }

        const message = loadError instanceof Error
          ? loadError.message
          : "Failed to load report data.";
        setError(message);
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [selectedLineId]);

  const lines = useMemo(
    () => [...(dashboard?.lines ?? [])].sort((a, b) => a.lineNumber - b.lineNumber),
    [dashboard?.lines]
  );

  const filteredRuns = useMemo(() => {
    const start = toDateRangeStart(startDate);
    const end = toDateRangeEnd(endDate);

    return runs.filter((row) => {
      const stamp = new Date(row.endTimeUtc).getTime();

      if (start && stamp < start.getTime()) {
        return false;
      }

      if (end && stamp > end.getTime()) {
        return false;
      }

      return true;
    });
  }, [runs, startDate, endDate]);

  const filteredEvents = useMemo(() => {
    const start = toDateRangeStart(startDate);
    const end = toDateRangeEnd(endDate);

    return events.filter((row) => {
      const stamp = new Date(row.occurredAtUtc).getTime();

      if (start && stamp < start.getTime()) {
        return false;
      }

      if (end && stamp > end.getTime()) {
        return false;
      }

      return true;
    });
  }, [events, startDate, endDate]);

  function handleLineFilterChange(event: SelectChangeEvent<string>) {
    setLineFilter(event.target.value);
  }

  function handleExportRunsCsv() {
    const rows = filteredRuns.map((row) => [
      String(row.lineNumber),
      row.lineName,
      row.productId || "N/A",
      row.finalStatus,
      row.startTimeUtc,
      row.endTimeUtc,
      formatDuration(row.runtimeSeconds),
      row.productionLength.toFixed(2),
      row.autoPercentage.toFixed(1),
      row.manualPercentage.toFixed(1),
    ]);

    downloadCsv(
      "completed-runs-report.csv",
      [
        "LineNumber",
        "LineName",
        "ProductId",
        "FinalStatus",
        "StartTimeUtc",
        "EndTimeUtc",
        "Runtime",
        "ProductionLength",
        "AutoPercentage",
        "ManualPercentage",
      ],
      rows
    );
  }

  function handleExportEventsCsv() {
    const rows = filteredEvents.map((row) => [
      String(row.lineNumber),
      row.lineName,
      row.eventType,
      row.previousValue,
      row.currentValue,
      row.occurredAtUtc,
    ]);

    downloadCsv(
      "runtime-switch-events.csv",
      [
        "LineNumber",
        "LineName",
        "EventType",
        "PreviousValue",
        "CurrentValue",
        "OccurredAtUtc",
      ],
      rows
    );
  }

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h4" sx={{ fontWeight: 700 }}>
          Reports
        </Typography>
        <Typography color="text.secondary">
          Run history and runtime switch events across all lines.
        </Typography>
      </Box>

      <Stack direction={{ xs: "column", md: "row" }} spacing={1.5}>
        <FormControl size="small" sx={{ minWidth: 260 }}>
          <InputLabel id="reports-line-filter-label">Line</InputLabel>
          <Select
            labelId="reports-line-filter-label"
            value={lineFilter}
            label="Line"
            onChange={handleLineFilterChange}
          >
            <MenuItem value="all">All lines</MenuItem>
            {lines.map((line) => (
              <MenuItem key={line.id} value={String(line.id)}>
                {`Line ${line.lineNumber} - ${line.lineName}`}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <TextField
          size="small"
          label="From"
          type="date"
          value={startDate}
          onChange={(event) => setStartDate(event.target.value)}
          InputLabelProps={{ shrink: true }}
        />

        <TextField
          size="small"
          label="To"
          type="date"
          value={endDate}
          onChange={(event) => setEndDate(event.target.value)}
          InputLabelProps={{ shrink: true }}
        />
      </Stack>

      {loading ? (
        <Alert severity="info">Loading reports...</Alert>
      ) : null}

      {error ? (
        <Alert severity="error">{error}</Alert>
      ) : null}

      <Paper sx={{ p: 2 }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          justifyContent="space-between"
          alignItems={{ xs: "flex-start", sm: "center" }}
          sx={{ mb: 1.5 }}
          spacing={1}
        >
          <Typography variant="h6">
            Completed Runs ({filteredRuns.length})
          </Typography>
          <Button size="small" variant="outlined" onClick={handleExportRunsCsv}>
            Export CSV
          </Button>
        </Stack>

        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Line</TableCell>
                <TableCell>Product</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Start</TableCell>
                <TableCell>End</TableCell>
                <TableCell>Runtime</TableCell>
                <TableCell>Length</TableCell>
                <TableCell>Auto %</TableCell>
                <TableCell>Manual %</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {filteredRuns.map((row) => (
                <TableRow key={row.id}>
                  <TableCell>{`L${row.lineNumber} - ${row.lineName}`}</TableCell>
                  <TableCell>{row.productId || "N/A"}</TableCell>
                  <TableCell>{row.finalStatus}</TableCell>
                  <TableCell>{formatDateTime(row.startTimeUtc)}</TableCell>
                  <TableCell>{formatDateTime(row.endTimeUtc)}</TableCell>
                  <TableCell>{formatDuration(row.runtimeSeconds)}</TableCell>
                  <TableCell>{row.productionLength.toFixed(2)}</TableCell>
                  <TableCell>{row.autoPercentage.toFixed(1)}%</TableCell>
                  <TableCell>{row.manualPercentage.toFixed(1)}%</TableCell>
                </TableRow>
              ))}

              {filteredRuns.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={9} align="center">
                    No completed runs for the selected filters.
                  </TableCell>
                </TableRow>
              ) : null}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          justifyContent="space-between"
          alignItems={{ xs: "flex-start", sm: "center" }}
          sx={{ mb: 1.5 }}
          spacing={1}
        >
          <Typography variant="h6">
            Mode and Status Switch Events ({filteredEvents.length})
          </Typography>
          <Button size="small" variant="outlined" onClick={handleExportEventsCsv}>
            Export CSV
          </Button>
        </Stack>

        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Timestamp</TableCell>
                <TableCell>Line</TableCell>
                <TableCell>Event Type</TableCell>
                <TableCell>From</TableCell>
                <TableCell>To</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {filteredEvents.map((row) => (
                <TableRow key={row.id}>
                  <TableCell>{formatDateTime(row.occurredAtUtc)}</TableCell>
                  <TableCell>{`L${row.lineNumber} - ${row.lineName}`}</TableCell>
                  <TableCell>{row.eventType}</TableCell>
                  <TableCell>{row.previousValue}</TableCell>
                  <TableCell>{row.currentValue}</TableCell>
                </TableRow>
              ))}

              {filteredEvents.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} align="center">
                    No switch events for the selected filters.
                  </TableCell>
                </TableRow>
              ) : null}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>
    </Stack>
  );
}