import {
  Alert,
  Box,
  Button,
  Chip,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";

import StatusChip from "../components/common/StatusChip";
import type { LineDetailModel, DiameterSensor } from "../models/LineDetailModel";
import { getLineDetail } from "../services/dashboardService";

export default function LineDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [detail, setDetail] = useState<LineDetailModel | null>(null);
  const [selectedSensorId, setSelectedSensorId] = useState<string | null>(null);

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

      setSelectedSensorId((previous) =>
        previous ?? nextDetail.diameterSensors[0]?.id ?? null
      );
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

  const selectedSensor = useMemo<DiameterSensor | null>(() => {
    if (!detail || !selectedSensorId) {
      return null;
    }

    return (
      detail.diameterSensors.find((sensor) => sensor.id === selectedSensorId) ??
      null
    );
  }, [detail, selectedSensorId]);

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
          Updated {new Date(detail.updatedAt).toLocaleTimeString()}
        </Typography>
      </Stack>

      <Paper sx={{ p: 2.5 }}>
        <Stack spacing={1}>
          <Typography variant="h4" sx={{ fontWeight: 700 }}>
            Line #{detail.line.lineNumber} - {detail.line.lineName}
          </Typography>

          <Stack direction="row" spacing={1} alignItems="center">
            <StatusChip status={detail.line.status} />
            <Chip label={`Serial ${detail.line.product}`} variant="outlined" />
            <Chip label={`PLC ${detail.line.plcIp}`} variant="outlined" />
          </Stack>
        </Stack>
      </Paper>

      <Stack direction={{ xs: "column", lg: "row" }} spacing={2}>
        <Paper sx={{ p: 2, flex: 1 }}>
          <Typography variant="h6" sx={{ mb: 1.5 }}>
            Diameter Sensors
          </Typography>

          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            {detail.diameterSensors.map((sensor) => (
              <Button
                key={sensor.id}
                size="small"
                variant={selectedSensorId === sensor.id ? "contained" : "outlined"}
                onClick={() => setSelectedSensorId(sensor.id)}
              >
                {sensor.label}
              </Button>
            ))}
          </Stack>

          {selectedSensor ? (
            <Box sx={{ mt: 2 }}>
              <Typography sx={{ fontWeight: 700 }}>{selectedSensor.label}</Typography>
              <Typography>
                Diameter: {selectedSensor.diameterMm.toFixed(3)} mm
              </Typography>
              <Typography>
                Deviation: {selectedSensor.deviationMm.toFixed(3)} mm
              </Typography>
              <Typography>Status: {selectedSensor.status}</Typography>
            </Box>
          ) : null}
        </Paper>

        <Paper sx={{ p: 2, flex: 1 }}>
          <Typography variant="h6" sx={{ mb: 1.5 }}>
            Pressure and Temperature
          </Typography>

          <Typography>Extruder Pressure: {detail.pressuresPsi.extruder} psi</Typography>
          <Typography>Die Head Pressure: {detail.pressuresPsi.dieHead} psi</Typography>
          <Typography>Cooling Pressure: {detail.pressuresPsi.cooling} psi</Typography>

          <Box sx={{ mt: 2 }}>
            <Typography>Zone 1 Temp: {detail.temperaturesC.zone1} C</Typography>
            <Typography>Zone 2 Temp: {detail.temperaturesC.zone2} C</Typography>
            <Typography>Zone 3 Temp: {detail.temperaturesC.zone3} C</Typography>
            <Typography>Die Temp: {detail.temperaturesC.die} C</Typography>
          </Box>
        </Paper>
      </Stack>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" sx={{ mb: 1.5 }}>
          Drive Speeds and Runtime
        </Typography>

        <Typography>Puller Speed: {detail.motorSpeedsRpm.puller} rpm</Typography>
        <Typography>Cutter Speed: {detail.motorSpeedsRpm.cutter} rpm</Typography>
        <Typography>Control Mode: {detail.line.controlMode}</Typography>
        <Typography>Runtime: {detail.line.runtime}</Typography>
        <Typography>Total Length: {detail.line.totalLength.toLocaleString()} ft</Typography>
      </Paper>
    </Stack>
  );
}
