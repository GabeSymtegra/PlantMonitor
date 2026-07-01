import { Box, Typography } from "@mui/material";

import { useDashboard } from "../context/DashboardContext";

import StatusSummary from "../components/dashboard/StatusSummary";
import SearchBar from "../components/dashboard/SearchBar";
import ViewToggle from "../components/dashboard/ViewToggle";
import LineTable from "../components/dashboard/LineTable";
import LineCards from "../components/dashboard/LineCards";

export default function Dashboard() {
  const { view } = useDashboard();

  return (
    <Box>
      <Typography
        variant="h4"
        sx={{
          mb: 4,
          fontWeight: 700,
        }}
      >
        Plant Status Dashboard
      </Typography>

      <StatusSummary />

      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          mb: 3,
          gap: 2,
        }}
      >
        <Box sx={{ flex: 1 }}>
          <SearchBar />
        </Box>

        <ViewToggle />
      </Box>

      {view === "table" ? <LineTable /> : <LineCards />}
    </Box>
  );
}