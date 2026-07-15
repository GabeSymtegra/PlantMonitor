import {
  Alert,
  Box,
  Button,
  Divider,
  LinearProgress,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";

import type {
  CompletedRunReportDetail,
  CompletedRunZoneStat,
  RuntimeEventReportRow,
} from "../models/Reports";
import {
  getCompletedRunReport,
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

function MetricRow({ label, value }: { label: string; value: string }) {
  return (
    <Stack direction="row" justifyContent="space-between" spacing={2}>
      <Typography color="text.secondary">{label}</Typography>
      <Typography sx={{ fontWeight: 600, textAlign: "right" }}>{value}</Typography>
    </Stack>
  );
}

function VarianceBar({ label, value }: { label: string; value: number }) {
  const safeValue = Math.max(0, Math.abs(value));
  const progress = Math.min(safeValue * 10, 100);

  return (
    <Stack spacing={0.5}>
      <Stack direction="row" justifyContent="space-between" spacing={2}>
        <Typography variant="body2" color="text.secondary">{label}</Typography>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>{value.toFixed(3)}%</Typography>
      </Stack>
      <LinearProgress variant="determinate" value={progress} sx={{ height: 8, borderRadius: 999 }} />
    </Stack>
  );
}

function ZoneSection({
  zone,
  stats,
}: {
  zone: string;
  stats: CompletedRunZoneStat[];
}) {
  const overall = stats.find((row) => row.segment === "overall");
  const auto = stats.find((row) => row.segment === "auto");
  const manual = stats.find((row) => row.segment === "manual");

  return (
    <Paper sx={{ p: 2, borderRadius: 3 }}>
      <Typography variant="h6" sx={{ fontWeight: 700, mb: 1.5 }}>
        {zone} Gauge Variance
      </Typography>

      {overall ? (
        <Stack spacing={1}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Overall
          </Typography>
          <VarianceBar label="Avg Abs Deviation" value={overall.averageAbsoluteDeviation} />
          <VarianceBar label="Max Positive" value={overall.maxPositiveDeviation} />
          <VarianceBar label="Max Negative" value={overall.maxNegativeDeviation} />
          <VarianceBar label="Current Deviation" value={overall.currentDeviation} />
        </Stack>
      ) : null}

      <Divider sx={{ my: 1.5 }} />

      <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
        {auto ? (
          <Box sx={{ flex: 1 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
              Auto
            </Typography>
            <Stack spacing={1}>
              <VarianceBar label="Avg Abs Deviation" value={auto.averageAbsoluteDeviation} />
              <VarianceBar label="Max Positive" value={auto.maxPositiveDeviation} />
              <VarianceBar label="Max Negative" value={auto.maxNegativeDeviation} />
            </Stack>
          </Box>
        ) : null}

        {manual ? (
          <Box sx={{ flex: 1 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
              Manual
            </Typography>
            <Stack spacing={1}>
              <VarianceBar label="Avg Abs Deviation" value={manual.averageAbsoluteDeviation} />
              <VarianceBar label="Max Positive" value={manual.maxPositiveDeviation} />
              <VarianceBar label="Max Negative" value={manual.maxNegativeDeviation} />
            </Stack>
          </Box>
        ) : null}
      </Stack>
    </Paper>
  );
}

export default function CompletedRunDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [run, setRun] = useState<CompletedRunReportDetail | null>(null);
  const [events, setEvents] = useState<RuntimeEventReportRow[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let disposed = false;

    async function load() {
      if (!id) {
        setLoading(false);
        return;
      }

      setLoading(true);

      const nextRun = await getCompletedRunReport(id);

      if (disposed || !nextRun) {
        setLoading(false);
        return;
      }

      const lineEvents = await getRuntimeEventReports(nextRun.lineId, 1000);
      const start = new Date(nextRun.startTimeUtc).getTime();
      const end = new Date(nextRun.endTimeUtc).getTime();

      const filteredEvents = lineEvents.filter((event) => {
        const stamp = new Date(event.occurredAtUtc).getTime();
        return stamp >= start && stamp <= end;
      });

      if (disposed) {
        return;
      }

      setRun(nextRun);
      setEvents(filteredEvents);
      setLoading(false);
    }

    void load();

    return () => {
      disposed = true;
    };
  }, [id]);

  const groupedZoneStats = useMemo(() => {
    const map = new Map<string, CompletedRunZoneStat[]>();

    for (const stat of run?.zoneStats ?? []) {
      const key = stat.zone;
      const existing = map.get(key) ?? [];
      existing.push(stat);
      map.set(key, existing);
    }

    return Array.from(map.entries());
  }, [run?.zoneStats]);

  if (loading) {
    return <Typography>Loading completed run details...</Typography>;
  }

  if (!run) {
    return <Alert severity="error">Completed run details were not found.</Alert>;
  }

  return (
    <Stack spacing={2.5}>
      <Stack direction="row" justifyContent="space-between" alignItems="center">
        <Button variant="outlined" onClick={() => navigate("/reports")}>Back to Completed Runs</Button>

        <Typography color="text.secondary">
          Run ended {formatDateTime(run.endTimeUtc)}
        </Typography>
      </Stack>

      <Paper sx={{ p: 2.5 }}>
        <Stack spacing={1}>
          <Typography variant="h4" sx={{ fontWeight: 700 }}>
            Line #{run.lineNumber} - {run.lineName}
          </Typography>
          <Typography color="text.secondary">
            Product {run.productId || "N/A"} | Recipe {run.recipeId} | Machine {run.machineId} | Operator {run.operatorName}
          </Typography>
          <Typography color="text.secondary">
            Final Status {run.finalStatus}
          </Typography>
        </Stack>
      </Paper>

      <Paper sx={{ p: 2.5 }}>
        <Stack spacing={1.1}>
          <Typography variant="h5" sx={{ fontWeight: 800 }}>
            Run Summary
          </Typography>
          <Divider />
          <MetricRow label="Start" value={formatDateTime(run.startTimeUtc)} />
          <MetricRow label="End" value={formatDateTime(run.endTimeUtc)} />
          <MetricRow label="Runtime" value={formatDuration(run.runtimeSeconds)} />
          <MetricRow label="Production Length" value={`${run.productionLength.toFixed(2)} ft`} />
          <MetricRow label="Auto Time" value={formatDuration(run.autoTimeSeconds)} />
          <MetricRow label="Manual Time" value={formatDuration(run.manualTimeSeconds)} />
          <MetricRow label="Auto %" value={`${run.autoPercentage.toFixed(1)}%`} />
          <MetricRow label="Manual %" value={`${run.manualPercentage.toFixed(1)}%`} />
        </Stack>
      </Paper>

      <Paper sx={{ p: 2.5 }}>
        <Stack spacing={1.25}>
          <Typography variant="h5" sx={{ fontWeight: 800 }}>
            Mode and Status Changes
          </Typography>
          {events.length === 0 ? (
            <Typography color="text.secondary">
              No mode or status changes were captured during this run window.
            </Typography>
          ) : (
            events.map((event) => (
              <Paper key={event.id} variant="outlined" sx={{ p: 1.5 }}>
                <Typography sx={{ fontWeight: 700 }}>
                  {event.eventType}
                </Typography>
                <Typography color="text.secondary">
                  {formatDateTime(event.occurredAtUtc)}
                </Typography>
                <Typography>
                  {event.previousValue} -&gt; {event.currentValue}
                </Typography>
              </Paper>
            ))
          )}
        </Stack>
      </Paper>

      <Stack spacing={1}>
        <Typography variant="h5" sx={{ fontWeight: 800 }}>
          Variance View
        </Typography>
        <Typography color="text.secondary">
          Saved variance metrics are grouped by gauge and by overall, auto, and manual segments.
        </Typography>
      </Stack>

      <Stack spacing={2}>
        {groupedZoneStats.map(([zone, stats]) => (
          <ZoneSection key={zone} zone={zone} stats={stats} />
        ))}
      </Stack>
    </Stack>
  );
}