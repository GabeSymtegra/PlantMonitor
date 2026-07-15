import { Box, Chip, Divider, Paper, Stack, Typography } from "@mui/material";
import { useNavigate } from "react-router-dom";

import StatusChip from "../common/StatusChip";

import { useDashboard } from "../../context/useDashboard";
import type { ProductionLine } from "../../types/ProductionLine";

function formatPercentOrUnknown(value: unknown, decimals = 1): string {
  const numberValue = Number(value);
  return Number.isFinite(numberValue) ? `${numberValue.toFixed(decimals)}%` : "??";
}

function formatDateTime(value: string): string {
  const parsed = new Date(value);

  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString([], {
    year: "numeric",
    month: "numeric",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

function MetricSummary({
  title,
  summary,
}: {
  title: string;
  summary: string;
}) {
  return (
    <Paper
      sx={{
        p: 1.25,
        minWidth: 0,
        borderRadius: 2,
        backgroundColor: (theme) => theme.palette.mode === "dark"
          ? "rgba(255,255,255,0.05)"
          : "rgba(15,23,42,0.03)",
        border: (theme) => `1px solid ${theme.palette.divider}`,
      }}
    >
      <Typography variant="subtitle2" sx={{ color: "text.secondary", fontWeight: 700, letterSpacing: 0.2, mb: 0.5 }}>
        {title}
      </Typography>

      <Typography variant="body2" sx={{ fontWeight: 600, lineHeight: 1.45 }}>
        {summary}
      </Typography>
    </Paper>
  );
}

interface LineTableProps {
  lines?: ProductionLine[];
}

export default function LineTable({ lines }: LineTableProps) {
  const { dashboard, loading } = useDashboard();
  const rows = lines ?? dashboard?.lines ?? [];
  const navigate = useNavigate();

  return (
    <Stack spacing={2}>
      {loading && rows.length === 0 ? (
        <Paper sx={{ p: 3 }}>
          <Typography>Loading dashboard lines...</Typography>
        </Paper>
      ) : null}

      {rows.map((line) => (
        <Paper
          key={line.id}
          sx={{
            p: 1.75,
            cursor: "pointer",
            border: (theme) => `1px solid ${theme.palette.divider}`,
            transition: "transform 120ms ease, box-shadow 120ms ease",
            "&:hover": {
              transform: "translateY(-2px)",
              boxShadow: 4,
            },
            borderRadius: 3,
          }}
          onClick={() => navigate(`/lines/${line.id}`)}
        >
          <Stack spacing={1.1}>
            <Stack
              direction={{ xs: "column", lg: "row" }}
              justifyContent="space-between"
              alignItems={{ xs: "flex-start", lg: "center" }}
              spacing={1.25}
            >
              <Box>
                <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                  Line #{line.lineNumber} - {line.lineName}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Serial: {line.product} | PLC: {line.plcIp} | Manufacturer: {line.manufacturer}
                </Typography>
              </Box>

              <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                <StatusChip status={line.status} />
                <Chip label={`Mode ${line.controlMode}`} variant="outlined" size="small" />
                <Chip label={`Time ${line.timeInStatus}`} variant="outlined" size="small" />
              </Stack>
            </Stack>

            <Divider />

            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  md: "repeat(3, minmax(0, 1fr))",
                },
                gap: 1.5,
              }}
            >
              <MetricSummary
                title="Runtime"
                summary={`Start ${formatDateTime(line.startDateTime)} | Length ${Number.isFinite(Number(line.totalLength)) ? `${line.totalLength.toLocaleString()} ft` : "??"} | Total Var ${formatPercentOrUnknown(line.totalVariance, 2)}`}
              />

              <MetricSummary
                title="Auto"
                summary={`Auto % ${formatPercentOrUnknown(line.percentAutoMode, 1)} | Variance ${formatPercentOrUnknown(line.autoVariance, 2)}`}
              />

              <MetricSummary
                title="Manual"
                summary={`Manual % ${formatPercentOrUnknown(line.percentManualMode, 1)} | Variance ${formatPercentOrUnknown(line.manualVariance, 2)}`}
              />
            </Box>
          </Stack>
        </Paper>
      ))}

      {!loading && rows.length === 0 ? (
        <Paper sx={{ p: 3 }}>
          <Typography>No dashboard lines are available.</Typography>
        </Paper>
      ) : null}
    </Stack>
  );
}