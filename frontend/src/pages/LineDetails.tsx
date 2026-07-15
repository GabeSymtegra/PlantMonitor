import {
  Alert,
  Button,
  Chip,
  Divider,
  Grid,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";

import StatusChip from "../components/common/StatusChip";
import type { GaugeDetail, LineDetailModel } from "../models/LineDetailModel";
import { getLineDetail } from "../services/dashboardService";

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

const gaugeCardColors = {
  Bare: {
    background: "#FFF7D6",
    border: "#E6CB69",
    accent: "#9A7A00",
  },
  Hot: {
    background: "#FDECEC",
    border: "#E9A5A5",
    accent: "#C75B5B",
  },
  Cold: {
    background: "#EAF4FF",
    border: "#9FC7F5",
    accent: "#2F6DB2",
  },
  Default: {
    background: "#F5F5F5",
    border: "#D0D0D0",
    accent: "#4B5563",
  },
} as const;

function MetricRow({ label, value }: { label: string; value: string }) {
  return (
    <Stack direction="row" justifyContent="space-between" spacing={2}>
      <Typography color="text.secondary">{label}</Typography>
      <Typography sx={{ fontWeight: 600, textAlign: "right" }}>{value}</Typography>
    </Stack>
  );
}

function GaugeSection({
  gauge,
}: {
  gauge: GaugeDetail;
}) {
  const palette = gaugeCardColors[gauge.zone as keyof typeof gaugeCardColors] ?? gaugeCardColors.Default;

  return (
    <Paper
      sx={{
        p: 2.5,
        height: "100%",
        backgroundColor: palette.background,
        border: `1px solid ${palette.border}`,
        borderRadius: 3,
      }}
    >
      <Typography variant="h6" sx={{ mb: 0.75, fontWeight: 800, color: palette.accent }}>
        {gauge.zone} Gauge
      </Typography>

      <Stack spacing={1}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, letterSpacing: 0.2 }}>
          Current Gauge Readings
        </Typography>
        <MetricRow label="Setpoint" value={gauge.currentSetpoint.toFixed(3)} />
        <MetricRow label="Actual" value={gauge.currentActual.toFixed(3)} />
        <MetricRow
          label="Deviation"
          value={`${gauge.currentPercentDeviation.toFixed(3)}%`}
        />
      </Stack>

      <Divider sx={{ my: 2.25, borderColor: palette.border }} />

      <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 700, letterSpacing: 0.2 }}>
        Overall Quality Metrics
      </Typography>
      <Stack spacing={1}>
        <MetricRow
          label="Avg Abs Deviation"
          value={`${gauge.overallAverageAbsoluteDeviation.toFixed(3)}%`}
        />
        <MetricRow
          label="Max Positive"
          value={`${gauge.overallMaxPositiveDeviation.toFixed(3)}%`}
        />
        <MetricRow
          label="Max Negative"
          value={`${gauge.overallMaxNegativeDeviation.toFixed(3)}%`}
        />
      </Stack>

      <Divider sx={{ my: 2.25, borderColor: palette.border }} />

      <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 700, letterSpacing: 0.2 }}>
        Mode Breakdown
      </Typography>

      <Stack spacing={1.5}>
        <Paper sx={{ p: 1.5, backgroundColor: "rgba(255,255,255,0.58)", borderRadius: 2 }}>
          <Typography variant="body2" sx={{ mb: 1, fontWeight: 700 }}>
            Auto Mode Quality
          </Typography>
          <Stack spacing={0.75}>
            <MetricRow
              label="Avg Abs Deviation"
              value={`${gauge.autoAverageAbsoluteDeviation.toFixed(3)}%`}
            />
            <MetricRow
              label="Max Positive"
              value={`${gauge.autoMaxPositiveDeviation.toFixed(3)}%`}
            />
            <MetricRow
              label="Max Negative"
              value={`${gauge.autoMaxNegativeDeviation.toFixed(3)}%`}
            />
          </Stack>
        </Paper>

        <Paper sx={{ p: 1.5, backgroundColor: "rgba(255,255,255,0.58)", borderRadius: 2 }}>
          <Typography variant="body2" sx={{ mb: 1, fontWeight: 700 }}>
            Manual Mode Quality
          </Typography>
          <Stack spacing={0.75}>
            <MetricRow
              label="Avg Abs Deviation"
              value={`${gauge.manualAverageAbsoluteDeviation.toFixed(3)}%`}
            />
            <MetricRow
              label="Max Positive"
              value={`${gauge.manualMaxPositiveDeviation.toFixed(3)}%`}
            />
            <MetricRow
              label="Max Negative"
              value={`${gauge.manualMaxNegativeDeviation.toFixed(3)}%`}
            />
          </Stack>
        </Paper>
      </Stack>
    </Paper>
  );
}

export default function LineDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [detail, setDetail] = useState<LineDetailModel | null>(null);

  const lineId = Number(id);

  useEffect(() => {
    if (!Number.isFinite(lineId)) {
      return;
    }

    let disposed = false;

    async function loadDetail() {
      const nextDetail = await getLineDetail(lineId);

      if (disposed || !nextDetail) {
        return;
      }

      setDetail(nextDetail);
    }

    void loadDetail();

    const timer = setInterval(() => {
      void loadDetail();
    }, 3000);

    return () => {
      disposed = true;
      clearInterval(timer);
    };
  }, [lineId]);

  if (!Number.isFinite(lineId)) {
    return <Alert severity="error">Invalid line id.</Alert>;
  }

  if (!detail) {
    return <Typography>Loading line details...</Typography>;
  }

  return (
    <Stack spacing={2.5}>
      <Stack direction="row" justifyContent="space-between" alignItems="center">
        <Button variant="outlined" onClick={() => navigate("/lines")}>
          Back to Lines
        </Button>

        <Typography color="text.secondary">
          Updated {new Date(detail.lastUpdated).toLocaleTimeString()}
        </Typography>
      </Stack>

      <Paper sx={{ p: 2.5 }}>
        <Stack spacing={1}>
          <Typography variant="h4" sx={{ fontWeight: 700 }}>
            Line #{detail.lineNumber} - {detail.lineName}
          </Typography>

          <Stack direction="row" spacing={1} alignItems="center">
            <StatusChip status={detail.status} />
            <Chip label={`Product ${detail.productId}`} variant="outlined" />
            <Chip label={`Recipe ${detail.recipeId}`} variant="outlined" />
            <Chip label={`PLC ${detail.plcIp}`} variant="outlined" />
          </Stack>

          <Typography color="text.secondary">
            Machine {detail.machineId} | Operator {detail.operatorName} | Mode {detail.controlMode}
          </Typography>
        </Stack>
      </Paper>

      <Paper sx={{ p: 2.5 }}>
        <Stack spacing={1.75}>
          <Typography variant="h5" sx={{ fontWeight: 800 }}>
            General Runtime Statistics
          </Typography>

          <Divider />

          <Stack spacing={1}>
            <MetricRow
              label="Production Length"
              value={`${detail.currentProductionLength.toLocaleString()} ft`}
            />
            <MetricRow label="Runtime" value={formatDuration(detail.runtimeSeconds)} />
            <MetricRow label="Auto Time" value={formatDuration(detail.autoTimeSeconds)} />
            <MetricRow label="Manual Time" value={formatDuration(detail.manualTimeSeconds)} />
          </Stack>

          <Divider />

          <Stack spacing={1}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, letterSpacing: 0.2 }}>
              Mode Percentages
            </Typography>
            <MetricRow label="Auto %" value={`${detail.autoPercentage.toFixed(2)}%`} />
            <MetricRow label="Manual %" value={`${detail.manualPercentage.toFixed(2)}%`} />
          </Stack>
        </Stack>
      </Paper>

      <Stack spacing={0.5}>
        <Typography variant="h5" sx={{ fontWeight: 800 }}>
          Gauge Quality Overview
        </Typography>
      </Stack>

      <Grid container spacing={2}>
        {detail.gauges.map((gauge) => (
          <Grid key={gauge.zone} size={{ xs: 12, lg: 4 }}>
            <GaugeSection gauge={gauge} />
          </Grid>
        ))}
      </Grid>
    </Stack>
  );
}
