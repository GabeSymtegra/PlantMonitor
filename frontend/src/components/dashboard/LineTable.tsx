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

  const columns: GridColDef[] = [
    {
      field: "lineNumber",
      headerName: "Line #",
      width: 110,
      sortable: false,
    },
    {
      field: "lineName",
      headerName: "Line Name",
      flex: 1,
      minWidth: 180,
      sortable: false,
    },
    {
      field: "status",
      headerName: "Status",
      width: 140,
      sortable: false,
      renderCell: (params) => <StatusChip status={params.value as "Running" | "Stopped" | "Bleedout" | "Startup" | "Faulted" | "Offline" | "Maintenance"} />,
    },
    {
      field: "controlMode",
      headerName: "Mode",
      width: 120,
      sortable: false,
      renderCell: (params) => <Chip label={`Mode ${params.value}`} variant="outlined" size="small" />,
    },
    {
      field: "plcIp",
      headerName: "PLC IP",
      width: 160,
      sortable: false,
    },
    {
      field: "manufacturer",
      headerName: "Manufacturer",
      width: 180,
      sortable: false,
    },
  ];

  const gridRows = rows.map((line) => ({
    id: line.id,
    lineNumber: line.lineNumber,
    lineName: line.lineName,
    status: line.status,
    controlMode: line.controlMode,
    plcIp: line.plcIp,
    manufacturer: line.manufacturer,
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
