import { Grid, Paper, Typography } from "@mui/material";

import { useDashboard } from "../../context/useDashboard";
import { LineStatus } from "../../types/LineStatus";

export default function StatusSummary() {
  const { dashboard } = useDashboard();

  const lines = dashboard?.lines ?? [];

  const statusCounts = lines.reduce(
    (counts, line) => {
      counts[line.status] += 1;
      return counts;
    },
    {
      [LineStatus.Running]: 0,
      [LineStatus.Stopped]: 0,
      [LineStatus.Bleedout]: 0,
      [LineStatus.Startup]: 0,
      [LineStatus.Faulted]: 0,
      [LineStatus.Offline]: 0,
      [LineStatus.Maintenance]: 0,
    }
  );

  const updatedValue = dashboard?.lastUpdated
    ? dashboard.lastUpdated.toLocaleTimeString([], {
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
      })
    : "--:--:--";

  const stats = [
    {
      title: "Running",
      value: statusCounts[LineStatus.Running],
      color: "#2E7D32",
    },
    {
      title: "Stopped",
      value: statusCounts[LineStatus.Stopped],
      color: "#ED6C02",
    },
    {
      title: "Faulted",
      value: statusCounts[LineStatus.Faulted],
      color: "#D32F2F",
    },
    {
      title: "Bleedout",
      value: statusCounts[LineStatus.Bleedout],
      color: "#0F766E",
    },
    {
      title: "Startup",
      value: statusCounts[LineStatus.Startup],
      color: "#7C3AED",
    },
    {
      title: "Offline",
      value: statusCounts[LineStatus.Offline],
      color: "#616161",
    },
    {
      title: "Maintenance",
      value: statusCounts[LineStatus.Maintenance],
      color: "#6A1B9A",
    },
    {
      title: "Updated",
      value: updatedValue,
      color: "#1565C0",
    },
  ];

  return (
    <Grid container spacing={2} sx={{ mb: 4 }}>
      {stats.map((item) => (
        <Grid key={item.title} size={{ xs: 12, sm: 6, md: 4 }}>
          <Paper
            elevation={3}
            sx={{
              p: 2,
              borderLeft: `6px solid ${item.color}`,
              borderRadius: 2,
            }}
          >
            <Typography variant="body2" color="text.secondary">
              {item.title}
            </Typography>

            <Typography
              variant="h4"
              sx={{
                fontWeight: 700,
              }}
            >
              {item.value}
            </Typography>
          </Paper>
        </Grid>
      ))}
    </Grid>
  );
}