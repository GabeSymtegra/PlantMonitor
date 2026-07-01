import { Grid, Paper, Typography } from "@mui/material";

const stats = [
  {
    title: "Running",
    value: 24,
    color: "#2E7D32",
  },
  {
    title: "Stopped",
    value: 2,
    color: "#ED6C02",
  },
  {
    title: "Faulted",
    value: 1,
    color: "#D32F2F",
  },
  {
    title: "Offline",
    value: 0,
    color: "#616161",
  },
  {
    title: "Updated",
    value: "12:42:18",
    color: "#1565C0",
  },
];

export default function StatusSummary() {
  return (
    <Grid container spacing={2} sx={{ mb: 4 }}>
      {stats.map((item) => (
        <Grid item xs={12} sm={6} md key={item.title}>
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