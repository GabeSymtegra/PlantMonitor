import {
  Alert,
  Box,
  Button,
  Chip,
  Checkbox,
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
import { useNavigate } from "react-router-dom";

import { useDashboard } from "../context/useDashboard";
import type {
  CompletedRunReportRow,
  RuntimeEventReportRow,
} from "../models/Reports";
import {
  deleteCompletedRunReport,
  getCompletedRunReports,
  getRuntimeEventReports,
} from "../services/reportsService";

type ReportsView = "runs" | "events";
type RunSort = "newest" | "oldest" | "lineAsc" | "lineDesc";
type DatePreset = "today" | "last7" | "last30" | "clear";

const REPORT_REFRESH_MS = 10000;
const RUNS_PAGE_SIZE = 25;
const EVENTS_PAGE_SIZE = 50;

// -----------------------------------------------------------------------------
// Formatting and export helpers
// -----------------------------------------------------------------------------

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

function escapeCsvCell(value: string): string {
  const normalized = /^[=+\-@]/.test(value) ? `'${value}` : value;
  const escaped = normalized.replace(/"/g, '""');
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

function toDateInputValue(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function resolveDatePresetRange(preset: DatePreset): { from: string; to: string } {
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());

  if (preset === "clear") {
    return { from: "", to: "" };
  }

  if (preset === "today") {
    const value = toDateInputValue(today);
    return { from: value, to: value };
  }

  const days = preset === "last7" ? 6 : 29;
  const from = new Date(today);
  from.setDate(today.getDate() - days);

  return {
    from: toDateInputValue(from),
    to: toDateInputValue(today),
  };
}

// Reports combines two historical views: completed runs and transition events.
export default function Reports() {
  const { dashboard } = useDashboard();
  const navigate = useNavigate();
  const [activeView, setActiveView] = useState<ReportsView>("runs");
  const [lineFilter, setLineFilter] = useState<string>("all");
  const [runSort, setRunSort] = useState<RunSort>("newest");
  const [startDate, setStartDate] = useState<string>("");
  const [endDate, setEndDate] = useState<string>("");
  const [runs, setRuns] = useState<CompletedRunReportRow[]>([]);
  const [events, setEvents] = useState<RuntimeEventReportRow[]>([]);
  const [runsTotalCount, setRunsTotalCount] = useState(0);
  const [eventsTotalCount, setEventsTotalCount] = useState(0);
  const [runPage, setRunPage] = useState(1);
  const [eventPage, setEventPage] = useState(1);
  const [selectedRunIds, setSelectedRunIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [deletingRunId, setDeletingRunId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const selectedLineId = lineFilter === "all" ? undefined : Number(lineFilter);

  useEffect(() => {
    setRunPage(1);
    setEventPage(1);
    setSelectedRunIds([]);
  }, [lineFilter, startDate, endDate]);

  // ---------------------------------------------------------------------------
  // Data loading
  // ---------------------------------------------------------------------------

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setError(null);

      try {
        const [runRows, eventRows] = await Promise.all([
          getCompletedRunReports({
            lineId: selectedLineId,
            fromDate: startDate || undefined,
            toDate: endDate || undefined,
            page: runPage,
            pageSize: RUNS_PAGE_SIZE,
          }),
          getRuntimeEventReports({
            lineId: selectedLineId,
            fromDate: startDate || undefined,
            toDate: endDate || undefined,
            page: eventPage,
            pageSize: EVENTS_PAGE_SIZE,
          }),
        ]);

        if (cancelled) {
          return;
        }

        setRuns(runRows.items);
        setRunsTotalCount(runRows.totalCount);
        setEvents(eventRows.items);
        setEventsTotalCount(eventRows.totalCount);
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

    const refreshTimer = setInterval(() => {
      void load();
    }, REPORT_REFRESH_MS);

    return () => {
      cancelled = true;
      clearInterval(refreshTimer);
    };
  }, [eventPage, runPage, selectedLineId, startDate, endDate]);

  // ---------------------------------------------------------------------------
  // Filtered views derived from raw API results
  // ---------------------------------------------------------------------------

  const lines = useMemo(
    () => [...(dashboard?.lines ?? [])].sort((a, b) => a.lineNumber - b.lineNumber),
    [dashboard?.lines]
  );

  const sortedRuns = useMemo(() => {
    const rows = [...runs];

    switch (runSort) {
      case "oldest":
        return rows.sort((left, right) =>
          new Date(left.endTimeUtc).getTime() - new Date(right.endTimeUtc).getTime()
        );
      case "lineAsc":
        return rows.sort((left, right) =>
          left.lineNumber - right.lineNumber || new Date(right.endTimeUtc).getTime() - new Date(left.endTimeUtc).getTime()
        );
      case "lineDesc":
        return rows.sort((left, right) =>
          right.lineNumber - left.lineNumber || new Date(right.endTimeUtc).getTime() - new Date(left.endTimeUtc).getTime()
        );
      case "newest":
      default:
        return rows.sort((left, right) =>
          new Date(right.endTimeUtc).getTime() - new Date(left.endTimeUtc).getTime()
        );
    }
  }, [runs, runSort]);

  const selectedRuns = useMemo(
    () => sortedRuns.filter((row) => selectedRunIds.includes(row.id)),
    [selectedRunIds, sortedRuns]
  );

  const allVisibleRunsSelected =
    sortedRuns.length > 0 && sortedRuns.every((row) => selectedRunIds.includes(row.id));

  const filteredEvents = events;

  const runAggregates = useMemo(() => {
    if (sortedRuns.length === 0) {
      return {
        totalLength: 0,
        averageRuntimeSeconds: 0,
        averageAutoPercentage: 0,
        statusCounts: new Map<string, number>(),
      };
    }

    const totalLength = sortedRuns.reduce((sum, row) => sum + row.productionLength, 0);
    const averageRuntimeSeconds =
      sortedRuns.reduce((sum, row) => sum + row.runtimeSeconds, 0) / sortedRuns.length;
    const averageAutoPercentage =
      sortedRuns.reduce((sum, row) => sum + row.autoPercentage, 0) / sortedRuns.length;

    const statusCounts = new Map<string, number>();
    for (const row of sortedRuns) {
      statusCounts.set(row.finalStatus, (statusCounts.get(row.finalStatus) ?? 0) + 1);
    }

    return {
      totalLength,
      averageRuntimeSeconds,
      averageAutoPercentage,
      statusCounts,
    };
  }, [sortedRuns]);

  const eventAggregates = useMemo(() => {
    const counts = new Map<string, number>();
    for (const row of filteredEvents) {
      counts.set(row.eventType, (counts.get(row.eventType) ?? 0) + 1);
    }

    return counts;
  }, [filteredEvents]);

  // ---------------------------------------------------------------------------
  // UI actions
  // ---------------------------------------------------------------------------

  function handleLineFilterChange(event: SelectChangeEvent<string>) {
    setLineFilter(event.target.value);
  }

  function handleRunSortChange(event: SelectChangeEvent<string>) {
    setRunSort(event.target.value as RunSort);
  }

  function handleDatePreset(preset: DatePreset) {
    const { from, to } = resolveDatePresetRange(preset);
    setStartDate(from);
    setEndDate(to);
  }

  function handleToggleRunSelection(runId: string) {
    setSelectedRunIds((current) =>
      current.includes(runId)
        ? current.filter((id) => id !== runId)
        : [...current, runId]
    );
  }

  function handleToggleSelectAllVisibleRuns() {
    if (allVisibleRunsSelected) {
      setSelectedRunIds((current) => current.filter((id) => !sortedRuns.some((row) => row.id === id)));
      return;
    }

    setSelectedRunIds((current) => Array.from(new Set([...current, ...sortedRuns.map((row) => row.id)])));
  }

  async function handleDeleteRun(runId: string) {
    setDeletingRunId(runId);

    try {
      await deleteCompletedRunReport(runId);
      setRuns((current) => current.filter((row) => row.id !== runId));
    } catch (deleteError) {
      const message = deleteError instanceof Error
        ? deleteError.message
        : "Failed to delete completed run.";
      setError(message);
    } finally {
      setDeletingRunId(null);
    }
  }

  async function handleDeleteSelectedRuns() {
    const runIds = [...selectedRunIds];

    const confirmed = window.confirm(
      `Delete ${runIds.length} completed run record${runIds.length === 1 ? "" : "s"}? This action is audited and cannot be undone from this screen.`
    );

    if (!confirmed) {
      return;
    }

    for (const runId of runIds) {
      await handleDeleteRun(runId);
    }

    setSelectedRunIds([]);
  }

  function handleKeepSelectedRuns() {
    setSelectedRunIds([]);
  }

  function handleExportRunsCsv() {
    const exportRows = selectedRuns.length > 0 ? selectedRuns : sortedRuns;
    const rows = exportRows.map((row) => [
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

  // ---------------------------------------------------------------------------
  // Page layout
  // ---------------------------------------------------------------------------

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h4" sx={{ fontWeight: 700 }}>
          Completed Runs
        </Typography>
        <Typography color="text.secondary">
          Saved production runs are recorded automatically when a line run ends.
        </Typography>
      </Box>

      <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
        <Button
          size="small"
          variant={activeView === "runs" ? "contained" : "outlined"}
          onClick={() => setActiveView("runs")}
        >
          Completed Runs
        </Button>
        <Button
          size="small"
          variant={activeView === "events" ? "contained" : "outlined"}
          onClick={() => setActiveView("events")}
        >
          Runtime Events
        </Button>
      </Stack>

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

        <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
          <Typography variant="caption" color="text.secondary">
            Presets
          </Typography>
          <Button size="small" variant="text" onClick={() => handleDatePreset("today")}>Today</Button>
          <Button size="small" variant="text" onClick={() => handleDatePreset("last7")}>Last 7d</Button>
          <Button size="small" variant="text" onClick={() => handleDatePreset("last30")}>Last 30d</Button>
          <Button size="small" variant="text" onClick={() => handleDatePreset("clear")}>Clear</Button>
        </Stack>

        {activeView === "runs" ? (
          <FormControl size="small" sx={{ minWidth: 220 }}>
            <InputLabel id="completed-runs-sort-label">Sort</InputLabel>
            <Select
              labelId="completed-runs-sort-label"
              value={runSort}
              label="Sort"
              onChange={handleRunSortChange}
            >
              <MenuItem value="newest">Newest first</MenuItem>
              <MenuItem value="oldest">Oldest first</MenuItem>
              <MenuItem value="lineAsc">Line number ascending</MenuItem>
              <MenuItem value="lineDesc">Line number descending</MenuItem>
            </Select>
          </FormControl>
        ) : null}
      </Stack>

      {loading ? (
        <Alert severity="info">Loading reports...</Alert>
      ) : null}

      {error ? (
        <Alert severity="error">{error}</Alert>
      ) : null}

      {activeView === "runs" ? (
        <Paper sx={{ p: 2 }}>
          <Stack direction={{ xs: "column", md: "row" }} spacing={1.5} sx={{ mb: 2 }}>
            <Alert severity="info" sx={{ flex: 1 }}>
              <Typography variant="body2">Total Length: {runAggregates.totalLength.toFixed(2)}</Typography>
            </Alert>
            <Alert severity="info" sx={{ flex: 1 }}>
              <Typography variant="body2">Average Runtime: {formatDuration(runAggregates.averageRuntimeSeconds)}</Typography>
            </Alert>
            <Alert severity="info" sx={{ flex: 1 }}>
              <Typography variant="body2">Average Auto %: {runAggregates.averageAutoPercentage.toFixed(1)}%</Typography>
            </Alert>
          </Stack>

          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ mb: 2 }}>
            {Array.from(runAggregates.statusCounts.entries()).map(([status, count]) => (
              <Chip key={status} label={`${status}: ${count}`} size="small" variant="outlined" />
            ))}
          </Stack>

          <Stack
            direction={{ xs: "column", sm: "row" }}
            justifyContent="space-between"
            alignItems={{ xs: "flex-start", sm: "center" }}
            sx={{ mb: 1.5 }}
            spacing={1}
          >
            <Box>
              <Typography variant="h6">
                Completed Runs ({runsTotalCount})
              </Typography>
              <Typography color="text.secondary">
                {selectedRunIds.length > 0
                  ? `${selectedRunIds.length} selected for bulk actions.`
                  : "Select completed runs to delete, keep, or export them in bulk."}
              </Typography>
            </Box>

            <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
              <Button
                size="small"
                variant="text"
                disabled={runPage <= 1 || loading}
                onClick={() => setRunPage((current) => Math.max(1, current - 1))}
              >
                Previous
              </Button>
              <Button
                size="small"
                variant="text"
                disabled={runPage * RUNS_PAGE_SIZE >= runsTotalCount || loading}
                onClick={() => setRunPage((current) => current + 1)}
              >
                Next
              </Button>
              <Button size="small" variant="outlined" onClick={handleExportRunsCsv}>
                {selectedRunIds.length > 0 ? "Export Selected" : "Export CSV"}
              </Button>
              <Button
                size="small"
                variant="outlined"
                disabled={selectedRunIds.length === 0}
                onClick={handleKeepSelectedRuns}
              >
                Keep Selected
              </Button>
              <Button
                size="small"
                color="error"
                variant="outlined"
                disabled={selectedRunIds.length === 0 || deletingRunId !== null}
                onClick={() => void handleDeleteSelectedRuns()}
              >
                Delete Selected
              </Button>
            </Stack>
          </Stack>

          <Typography color="text.secondary" sx={{ mb: 1 }}>
            {`Page ${runPage} of ${Math.max(1, Math.ceil(runsTotalCount / RUNS_PAGE_SIZE))}`}
          </Typography>

          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell padding="checkbox">
                    <Checkbox
                      checked={allVisibleRunsSelected}
                      indeterminate={selectedRunIds.length > 0 && !allVisibleRunsSelected}
                      onChange={handleToggleSelectAllVisibleRuns}
                    />
                  </TableCell>
                  <TableCell>Line</TableCell>
                  <TableCell>Product</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Start</TableCell>
                  <TableCell>End</TableCell>
                  <TableCell>Runtime</TableCell>
                  <TableCell>Length</TableCell>
                  <TableCell>Auto %</TableCell>
                  <TableCell>Manual %</TableCell>
                  <TableCell align="right">Select</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {sortedRuns.map((row) => (
                  <TableRow
                    key={row.id}
                    hover
                    selected={selectedRunIds.includes(row.id)}
                    sx={{ cursor: "pointer" }}
                    onClick={() => navigate(`/reports/completed/${row.id}`)}
                  >
                    <TableCell padding="checkbox" onClick={(event) => event.stopPropagation()}>
                      <Checkbox
                        checked={selectedRunIds.includes(row.id)}
                        onChange={() => handleToggleRunSelection(row.id)}
                      />
                    </TableCell>
                    <TableCell>{`L${row.lineNumber} - ${row.lineName}`}</TableCell>
                    <TableCell>{row.productId || "N/A"}</TableCell>
                    <TableCell>{row.finalStatus}</TableCell>
                    <TableCell>{formatDateTime(row.startTimeUtc)}</TableCell>
                    <TableCell>{formatDateTime(row.endTimeUtc)}</TableCell>
                    <TableCell>{formatDuration(row.runtimeSeconds)}</TableCell>
                    <TableCell>{row.productionLength.toFixed(2)}</TableCell>
                    <TableCell>{row.autoPercentage.toFixed(1)}%</TableCell>
                    <TableCell>{row.manualPercentage.toFixed(1)}%</TableCell>
                    <TableCell align="right">
                      <Button
                        size="small"
                        variant="outlined"
                        disabled={deletingRunId !== null}
                        onClick={(event) => {
                          event.stopPropagation();
                          handleToggleRunSelection(row.id);
                        }}
                      >
                        {selectedRunIds.includes(row.id) ? "Selected" : "Select"}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}

                {sortedRuns.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={11} align="center">
                      No completed runs for the selected filters.
                    </TableCell>
                  </TableRow>
                ) : null}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      ) : (
        <Paper sx={{ p: 2 }}>
          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ mb: 2 }}>
            {Array.from(eventAggregates.entries()).map(([eventType, count]) => (
              <Chip key={eventType} label={`${eventType}: ${count}`} size="small" variant="outlined" />
            ))}
            {eventAggregates.size === 0 ? (
              <Chip label="No events in current filter" size="small" variant="outlined" />
            ) : null}
          </Stack>

          <Stack
            direction={{ xs: "column", sm: "row" }}
            justifyContent="space-between"
            alignItems={{ xs: "flex-start", sm: "center" }}
            sx={{ mb: 1.5 }}
            spacing={1}
          >
            <Typography variant="h6">
              Runtime Events ({eventsTotalCount})
            </Typography>
            <Stack direction="row" spacing={1}>
              <Button
                size="small"
                variant="text"
                disabled={eventPage <= 1 || loading}
                onClick={() => setEventPage((current) => Math.max(1, current - 1))}
              >
                Previous
              </Button>
              <Button
                size="small"
                variant="text"
                disabled={eventPage * EVENTS_PAGE_SIZE >= eventsTotalCount || loading}
                onClick={() => setEventPage((current) => current + 1)}
              >
                Next
              </Button>
              <Button size="small" variant="outlined" onClick={handleExportEventsCsv}>
                Export CSV
              </Button>
            </Stack>
          </Stack>

          <Typography color="text.secondary" sx={{ mb: 1 }}>
            {`Page ${eventPage} of ${Math.max(1, Math.ceil(eventsTotalCount / EVENTS_PAGE_SIZE))}`}
          </Typography>

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
                      No runtime events for the selected filters.
                    </TableCell>
                  </TableRow>
                ) : null}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      )}
    </Stack>
  );
}