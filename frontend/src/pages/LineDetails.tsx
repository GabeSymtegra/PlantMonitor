import {
  Alert,
  Button,
  Chip,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";

import StatusChip from "../components/common/StatusChip";
import type { LineDetailModel, SensorDetail } from "../models/LineDetailModel";
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

function SensorSection({ sensor }: { sensor: SensorDetail }) {
  return (
    <Paper sx={{ p: 2.5, flex: 1 }}>
      <Typography variant="h6" sx={{ mb: 1.5, fontWeight: 700 }}>
        {sensor.zone} Sensor
      </Typography>

      <Stack spacing={0.6}>
        <Typography>Current Setpoint: {sensor.currentSetpoint.toFixed(3)}</Typography>
        <Typography>Current Actual: {sensor.currentActual.toFixed(3)}</Typography>
        <Typography>
          Current % Deviation: {sensor.currentPercentDeviation.toFixed(3)}%
        </Typography>
      </Stack>

      <Typography variant="subtitle2" sx={{ mt: 2, mb: 1, color: "text.secondary" }}>
        Overall Quality
      </Typography>
      <Stack spacing={0.6}>
        <Typography>Count: {sensor.overallMeasurementCount}</Typography>
        <Typography>
          Avg Abs Deviation: {sensor.overallAverageAbsoluteDeviation.toFixed(3)}%
        </Typography>
        <Typography>
          Max Positive: {sensor.overallMaxPositiveDeviation.toFixed(3)}%
        </Typography>
        <Typography>
          Max Negative: {sensor.overallMaxNegativeDeviation.toFixed(3)}%
        </Typography>
      </Stack>

      <Typography variant="subtitle2" sx={{ mt: 2, mb: 1, color: "text.secondary" }}>
        Auto Mode
      </Typography>
      <Stack spacing={0.6}>
        <Typography>Count: {sensor.autoMeasurementCount}</Typography>
        <Typography>
          Avg Abs Deviation: {sensor.autoAverageAbsoluteDeviation.toFixed(3)}%
        </Typography>
        <Typography>
          Max Positive: {sensor.autoMaxPositiveDeviation.toFixed(3)}%
        </Typography>
        <Typography>
          Max Negative: {sensor.autoMaxNegativeDeviation.toFixed(3)}%
        </Typography>
      </Stack>

      <Typography variant="subtitle2" sx={{ mt: 2, mb: 1, color: "text.secondary" }}>
        Manual Mode
      </Typography>
      <Stack spacing={0.6}>
        <Typography>Count: {sensor.manualMeasurementCount}</Typography>
        <Typography>
          Avg Abs Deviation: {sensor.manualAverageAbsoluteDeviation.toFixed(3)}%
        </Typography>
        <Typography>
          Max Positive: {sensor.manualMaxPositiveDeviation.toFixed(3)}%
        </Typography>
        <Typography>
          Max Negative: {sensor.manualMaxNegativeDeviation.toFixed(3)}%
        </Typography>
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
        <Stack spacing={0.6}>
          <Typography>Production Length: {detail.currentProductionLength.toLocaleString()} ft</Typography>
          <Typography>Runtime: {formatDuration(detail.runtimeSeconds)}</Typography>
          <Typography>Auto Time: {formatDuration(detail.autoTimeSeconds)}</Typography>
          <Typography>Manual Time: {formatDuration(detail.manualTimeSeconds)}</Typography>
          <Typography>Auto %: {detail.autoPercentage.toFixed(2)}%</Typography>
          <Typography>Manual %: {detail.manualPercentage.toFixed(2)}%</Typography>
        </Stack>
      </Paper>

      <Stack direction={{ xs: "column", lg: "row" }} spacing={2}>
        {detail.sensors.map((sensor) => (
          <SensorSection key={sensor.zone} sensor={sensor} />
        ))}
      </Stack>
    </Stack>
  );
}
