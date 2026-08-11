import { Box, Chip, Paper, Stack, Typography } from "@mui/material";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { useNavigate } from "react-router-dom";

import StatusChip from "../common/StatusChip";

import { useDashboard } from "../../context/useDashboard";
import type { ProductionLine } from "../../types/ProductionLine";

interface LineTableProps {
  lines?: ProductionLine[];
}

export default function LineTable({ lines }: LineTableProps) {
  const { dashboard, loading } = useDashboard();
  const rows = lines ?? dashboard?.lines ?? [];
  const navigate = useNavigate();

  function displayOrUnknown(value: string | null | undefined): string {
    const trimmed = value?.trim();
    return trimmed && trimmed.length > 0 ? trimmed : "Unknown";
  }

  function formatPercent(value: unknown): string {
    const numberValue = Number(value);
    return Number.isFinite(numberValue) ? `${numberValue.toFixed(1)}%` : "??";
  }

  function formatVariance(value: unknown): string {
    const numberValue = Number(value);
    return Number.isFinite(numberValue) ? numberValue.toFixed(2) : "??";
  }

  function formatLength(value: unknown): string {
    const numberValue = Number(value);
    return Number.isFinite(numberValue) ? `${numberValue.toLocaleString()} ft` : "??";
  }

  const columns: GridColDef[] = [
    {
      field: "line",
      headerName: "Line",
      minWidth: 220,
      width: 260,
      sortable: false,
      align: "center",
      headerAlign: "center",
    },
    {
      field: "status",
      headerName: "Status",
      width: 140,
      sortable: false,
      align: "center",
      headerAlign: "center",
      renderCell: (params) => (
        <Box
          sx={{
            width: "100%",
            height: "100%",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            lineHeight: 1,
          }}
        >
          <StatusChip status={params.value as "Running" | "Stopped" | "Bleedout" | "Startup" | "Faulted" | "Offline" | "Maintenance"} />
        </Box>
      ),
    },
    {
      field: "serial",
      headerName: "Serial",
      minWidth: 190,
      width: 210,
      sortable: false,
      align: "center",
      headerAlign: "center",
    },
    {
      field: "time",
      headerName: "Time",
      width: 130,
      sortable: false,
      align: "center",
      headerAlign: "center",
    },
    {
      field: "controlMode",
      headerName: "Control",
      width: 120,
      sortable: false,
      align: "center",
      headerAlign: "center",
      renderCell: (params) => (
        <Box
          sx={{
            width: "100%",
            height: "100%",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            lineHeight: 1,
          }}
        >
          <Chip label={String(params.value)} variant="outlined" size="small" />
        </Box>
      ),
    },
    {
      field: "length",
      headerName: "Length",
      width: 130,
      sortable: false,
      align: "center",
      headerAlign: "center",
    },
    {
      field: "percentAuto",
      headerName: "% Auto",
      width: 120,
      sortable: false,
      align: "center",
      headerAlign: "center",
    },
    {
      field: "autoVar",
      headerName: "Auto Var",
      width: 120,
      sortable: false,
      align: "center",
      headerAlign: "center",
    },
    {
      field: "percentMan",
      headerName: "% Man",
      width: 120,
      sortable: false,
      align: "center",
      headerAlign: "center",
    },
    {
      field: "manVar",
      headerName: "Man Var",
      width: 120,
      sortable: false,
      align: "center",
      headerAlign: "center",
    },
    {
      field: "totalVar",
      headerName: "Total Var",
      width: 130,
      sortable: false,
      align: "center",
      headerAlign: "center",
    },
  ];

  const gridRows = rows.map((line) => ({
    id: line.id,
    line: `#${line.lineNumber} ${line.lineName}`,
    status: line.status,
    serial: displayOrUnknown(line.product),
    time: line.timeInStatus,
    controlMode: line.controlMode,
    length: formatLength(line.totalLength),
    percentAuto: formatPercent(line.percentAutoMode),
    autoVar: formatVariance(line.autoVariance),
    percentMan: formatPercent(line.percentManualMode),
    manVar: formatVariance(line.manualVariance),
    totalVar: formatVariance(line.totalVariance),
  }));

  return (
    <Stack spacing={2}>
      {loading && rows.length === 0 ? (
        <Paper sx={{ p: 3 }}>
          <Typography>Loading dashboard lines...</Typography>
        </Paper>
      ) : null}

      <Box sx={{ height: 420, width: "100%" }}>
        <DataGrid
          rows={gridRows}
          columns={columns}
          autoHeight
          disableRowSelectionOnClick
          disableColumnMenu
          onRowClick={(params) => navigate(`/lines/${params.row.id}`)}
          initialState={{
            pagination: {
              paginationModel: { pageSize: 10, page: 0 },
            },
          }}
          pageSizeOptions={[10]}
          sx={{
            border: 0,
            borderRadius: 2,
            "& .MuiDataGrid-columnHeaders": {
              backgroundColor: (theme) => theme.palette.mode === "dark" ? "rgba(255,255,255,0.06)" : "rgba(0,0,0,0.04)",
            },
            "& .MuiDataGrid-columnHeaderTitleContainer": {
              justifyContent: "center",
            },
            "& .MuiDataGrid-cell": {
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              textAlign: "center",
            },
          }}
        />
      </Box>

      {!loading && rows.length === 0 ? (
        <Paper sx={{ p: 3 }}>
          <Typography>No dashboard lines are available.</Typography>
        </Paper>
      ) : null}
    </Stack>
  );
}
