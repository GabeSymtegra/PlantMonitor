import { Alert, Box, Button, Typography } from "@mui/material";
import { useMemo, useState } from "react";

import { useDashboard } from "../context/useDashboard";

import StatusSummary from "../components/dashboard/StatusSummary";
import SearchBar from "../components/dashboard/SearchBar";
import ViewToggle from "../components/dashboard/ViewToggle";
import LineTable from "../components/dashboard/LineTable";
import LineCards from "../components/dashboard/LineCards";

export default function Dashboard() {
  const { view, dashboard, error, refresh } = useDashboard();
  const [searchTerm, setSearchTerm] = useState("");

  const visibleLines = useMemo(() => {
    const lines = dashboard?.lines ?? [];
    const normalizedSearch = searchTerm.trim().toLowerCase();

    if (!normalizedSearch) {
      return lines;
    }

    return lines.filter((line) => {
      const searchTargets = [
        line.lineNumber.toString(),
        line.product,
        line.status,
        line.controlMode,
        line.plcIp,
        line.manufacturer,
      ];

      return searchTargets.some((target) =>
        target.toLowerCase().includes(normalizedSearch)
      );
    });
  }, [dashboard?.lines, searchTerm]);

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
          <SearchBar value={searchTerm} onChange={setSearchTerm} />
        </Box>

        <ViewToggle />
      </Box>

      {error ? (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          action={
            <Button color="inherit" size="small" onClick={() => void refresh()}>
              Retry
            </Button>
          }
        >
          {error}
        </Alert>
      ) : null}

      {view === "table" ? <LineTable lines={visibleLines} /> : <LineCards lines={visibleLines} />}
    </Box>
  );
}