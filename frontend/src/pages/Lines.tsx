import { Box, Button, Chip, Paper, Stack, Typography } from "@mui/material";
import { useMemo } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

import { useDashboard } from "../context/useDashboard";
import { LineStatus } from "../types/LineStatus";

const statusOrder: LineStatus[] = [
  LineStatus.Running,
  LineStatus.Stopped,
  LineStatus.Faulted,
  LineStatus.Offline,
  LineStatus.Maintenance,
];

export default function Lines() {
  const { dashboard } = useDashboard();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const requestedStatus = searchParams.get("status");
  const statusFilter = statusOrder.includes(requestedStatus as LineStatus)
    ? (requestedStatus as LineStatus)
    : null;

  const lines = dashboard?.lines ?? [];

  const visibleLines = useMemo(() => {
    if (!statusFilter) {
      return lines;
    }

    return lines.filter((line) => line.status === statusFilter);
  }, [lines, statusFilter]);

  function handleSetStatus(status: LineStatus | null) {
    if (!status) {
      setSearchParams({});
      return;
    }

    setSearchParams({ status });
  }

  return (
    <Stack spacing={2.5}>
      <Stack
        direction={{ xs: "column", md: "row" }}
        justifyContent="space-between"
        alignItems={{ xs: "flex-start", md: "center" }}
        spacing={2}
      >
        <Typography variant="h4" sx={{ fontWeight: 700 }}>
          Production Lines
        </Typography>

        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
          <Button
            size="small"
            variant={statusFilter ? "outlined" : "contained"}
            onClick={() => handleSetStatus(null)}
          >
            All
          </Button>

          {statusOrder.map((status) => (
            <Button
              key={status}
              size="small"
              variant={statusFilter === status ? "contained" : "outlined"}
              onClick={() => handleSetStatus(status)}
            >
              {status}
            </Button>
          ))}
        </Stack>
      </Stack>

      <Typography color="text.secondary">
        Showing {visibleLines.length} line{visibleLines.length === 1 ? "" : "s"}
        {statusFilter ? ` with status ${statusFilter}` : ""}.
      </Typography>

      {visibleLines.map((line) => (
        <Paper
          key={line.id}
          sx={{
            p: 2,
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            gap: 2,
            cursor: "pointer",
          }}
          onClick={() => navigate(`/lines/${line.id}`)}
        >
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
              Line #{line.lineNumber} - {line.lineName}
            </Typography>
            <Typography color="text.secondary">
              Serial: {line.product} | PLC: {line.plcIp} | Manufacturer: {line.manufacturer}
            </Typography>
          </Box>

          <Chip label={line.status} color={
            line.status === LineStatus.Running
              ? "success"
              : line.status === LineStatus.Stopped
                ? "warning"
                : line.status === LineStatus.Faulted
                  ? "error"
                  : line.status === LineStatus.Maintenance
                    ? "info"
                    : "default"
          } />
        </Paper>
      ))}

      {!visibleLines.length ? (
        <Paper sx={{ p: 3 }}>
          <Typography>No lines match the selected filter.</Typography>
        </Paper>
      ) : null}
    </Stack>
  );
}
