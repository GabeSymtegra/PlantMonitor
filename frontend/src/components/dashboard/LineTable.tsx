import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { useNavigate } from "react-router-dom";

import StatusChip from "../common/StatusChip";

import { useDashboard } from "../../context/useDashboard";
import { useThemeMode } from "../../context/useThemeMode";

import type { LineStatus } from "../../types/LineStatus";
import type { ProductionLine } from "../../types/ProductionLine";

function formatDateTime(value: string): string {
  const parsed = new Date(value);

  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString();
}

const baseColumns: GridColDef[] = [
  {
    field: "lineNumber",
    headerName: "Line #",
    width: 85,
  },
  {
    field: "lineName",
    headerName: "Line Name",
    minWidth: 165,
    width: 185,
  },
  {
    field: "product",
    headerName: "Product Serial",
    minWidth: 160,
    width: 180,
  },
  {
    field: "startDateTime",
    headerName: "Start Date:Time",
    minWidth: 210,
    width: 230,
    valueFormatter: (value) => formatDateTime(String(value ?? "")),
  },
  {
    field: "status",
    headerName: "Status",
    width: 145,
    renderCell: (params) => (
      <StatusChip status={params.value as LineStatus} />
    ),
  },
  {
    field: "timeInStatus",
    headerName: "Time",
    width: 130,
  },
  {
    field: "totalLength",
    headerName: "Total Length",
    width: 125,
    valueFormatter: (value) => `${Number(value ?? 0).toLocaleString()} ft`,
  },
  {
    field: "controlMode",
    headerName: "Control Mode",
    width: 120,
  },
  {
    field: "percentAutoMode",
    headerName: "%Auto Mode",
    width: 115,
    valueFormatter: (value) => `${Number(value ?? 0).toFixed(1)}%`,
  },
  {
    field: "autoVariance",
    headerName: "Auto Variance",
    width: 120,
    valueFormatter: (value) => Number(value ?? 0).toFixed(2),
  },
  {
    field: "percentManualMode",
    headerName: "%Man Mode",
    width: 115,
    valueFormatter: (value) => `${Number(value ?? 0).toFixed(1)}%`,
  },
  {
    field: "manualVariance",
    headerName: "Man Variance",
    width: 120,
    valueFormatter: (value) => Number(value ?? 0).toFixed(2),
  },
  {
    field: "totalVariance",
    headerName: "Total Variance",
    width: 125,
    valueFormatter: (value) => Number(value ?? 0).toFixed(2),
  },
];

const columns: GridColDef[] = baseColumns.map((column) => ({
  ...column,
  headerAlign: "center",
}));

interface LineTableProps {
  lines?: ProductionLine[];
}

export default function LineTable({ lines }: LineTableProps) {
  const { dashboard, loading } = useDashboard();
  const { appearance } = useThemeMode();
  const rows = lines ?? dashboard?.lines ?? [];
  const navigate = useNavigate();

  return (
    <div
      style={{
        height: 550,
        width: "100%",
      }}
    >
      <DataGrid
        rows={rows}
        columns={columns}
        loading={loading}
        sx={{
          border: (theme) => `1px solid ${theme.palette.divider}`,
          backgroundColor: (theme) => theme.palette.background.paper,
          "& .MuiDataGrid-columnHeaders": {
            backgroundColor: appearance.tableColor,
          },
          "& .MuiDataGrid-columnHeaderTitle": {
            fontWeight: appearance.boldText ? 700 : 600,
          },
        }}
        pageSizeOptions={[10, 25, 50]}
        initialState={{
          pagination: {
            paginationModel: {
              pageSize: 10,
              page: 0,
            },
          },
        }}
        onRowClick={(params) => navigate(`/lines/${params.row.id}`)}
        disableRowSelectionOnClick
      />
    </div>
  );
}