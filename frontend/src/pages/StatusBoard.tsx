import {
  Box,
  Chip,
  Paper,
  Stack,
  Typography,
  Button,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import LogoutIcon from "@mui/icons-material/Logout";
import { useMemo } from "react";
import { useNavigate } from "react-router-dom";

import { useAuth } from "../context/useAuth";
import { useDashboard } from "../context/useDashboard";
import { useThemeMode } from "../context/useThemeMode";
import { LineStatus } from "../types/LineStatus";

const statusColorMap: Record<LineStatus, string> = {
  [LineStatus.Running]: "#2E7D32",
  [LineStatus.Stopped]: "#ED6C02",
  [LineStatus.Faulted]: "#D32F2F",
  [LineStatus.Offline]: "#616161",
  [LineStatus.Maintenance]: "#1565C0",
};

function formatNumber(value: number) {
  return Number(value).toLocaleString();
}

export default function StatusBoard() {
  const navigate = useNavigate();
  const { logout, isAuthenticated } = useAuth();
  const { dashboard, loading } = useDashboard();
  const { appearance } = useThemeMode();

  const monitorName = appearance.monitorName.trim() || "Plant";

  const updatedValue = dashboard?.lastUpdated
    ? dashboard.lastUpdated.toLocaleTimeString([], {
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
      })
    : "--:--:--";

  const lines = dashboard?.lines ?? [];

  const sortedLines = useMemo(
    () => [...lines].sort((a, b) => a.lineNumber - b.lineNumber),
    [lines]
  );

  return (
    <Box
      sx={{
        minHeight: "100vh",
        backgroundColor: "background.default",
        p: { xs: 1.5, md: 2 },
      }}
    >
      <Stack spacing={1.5}>
        <Stack
          direction="row"
          justifyContent="space-between"
          alignItems="center"
          spacing={2}
        >
          <Typography
            variant="h4"
            sx={{
              fontWeight: 800,
              letterSpacing: 0.8,
              textTransform: "uppercase",
              lineHeight: 1.1,
            }}
          >
            {monitorName} Status Board
          </Typography>

          <Stack direction="row" spacing={1.25} alignItems="center">
            <Chip
              label={loading ? "SYNCING" : "LIVE"}
              color={loading ? "warning" : "success"}
              size="small"
              sx={{ fontWeight: 700, minWidth: 78 }}
            />

            <Chip
              label={`Updated ${updatedValue}`}
              size="small"
              sx={{
                fontWeight: 700,
                bgcolor: "rgba(255,255,255,0.14)",
              }}
            />

            <Button
              variant="outlined"
              size="small"
              startIcon={<ArrowBackIcon />}
              onClick={() => navigate("/")}
            >
              Back to App
            </Button>

            {isAuthenticated ? (
              <Button
                variant="contained"
                size="small"
                color="secondary"
                startIcon={<LogoutIcon />}
                onClick={() => {
                  logout();
                  navigate("/login", { replace: true });
                }}
              >
                Logout
              </Button>
            ) : null}
          </Stack>
        </Stack>

        <Paper
          elevation={3}
          sx={{
            overflow: "hidden",
            border: (theme) => `1px solid ${theme.palette.divider}`,
          }}
        >
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns:
                "1fr 1fr 1.1fr 1fr 1fr 0.9fr 0.9fr 0.95fr 0.95fr 0.95fr 1.15fr",
              backgroundColor: "primary.main",
              color: "primary.contrastText",
              p: 1.4,
              fontWeight: 800,
              textTransform: "uppercase",
              letterSpacing: 0.4,
              textAlign: "center",
            }}
          >
            <Typography>Line</Typography>
            <Typography>Status</Typography>
            <Typography>Serial</Typography>
            <Typography>Time</Typography>
            <Typography>Control</Typography>
            <Typography>Length</Typography>
            <Typography>% Auto</Typography>
            <Typography>Auto Var</Typography>
            <Typography>% Man</Typography>
            <Typography>Man Var</Typography>
            <Typography>Total Var</Typography>
          </Box>

          <Stack>
            {sortedLines.map((line) => (
              <Box
                key={line.id}
                sx={{
                  display: "grid",
                  gridTemplateColumns:
                    "1fr 1fr 1.1fr 1fr 1fr 0.9fr 0.9fr 0.95fr 0.95fr 0.95fr 1.15fr",
                  alignItems: "center",
                  px: 1.4,
                  py: 1,
                  borderTop: (theme) => `1px solid ${theme.palette.divider}`,
                }}
              >
                <Typography fontWeight={700}>
                  #{line.lineNumber} {line.lineName}
                </Typography>

                <Chip
                  label={line.status}
                  size="small"
                  sx={{
                    justifySelf: "start",
                    color: "#fff",
                    bgcolor: statusColorMap[line.status],
                    fontWeight: 700,
                  }}
                />

                <Typography>{line.product}</Typography>
                <Typography>{line.timeInStatus}</Typography>
                <Typography fontWeight={700}>{line.controlMode}</Typography>
                <Typography fontWeight={700}>
                  {formatNumber(line.totalLength)} ft
                </Typography>
                <Typography align="center">{line.percentAutoMode.toFixed(1)}%</Typography>
                <Typography align="center">{line.autoVariance.toFixed(2)}</Typography>
                <Typography align="center">{line.percentManualMode.toFixed(1)}%</Typography>
                <Typography align="center">{line.manualVariance.toFixed(2)}</Typography>
                <Typography align="center" fontWeight={700}>
                  {line.totalVariance.toFixed(2)}
                </Typography>
              </Box>
            ))}

            {!sortedLines.length ? (
              <Box sx={{ p: 3 }}>
                <Typography variant="h6">No line data available.</Typography>
              </Box>
            ) : null}
          </Stack>
        </Paper>
      </Stack>
    </Box>
  );
}
