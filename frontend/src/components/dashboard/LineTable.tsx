import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { getContrastRatio } from "@mui/material/styles";
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

function formatNumberOrUnknown(value: unknown, decimals = 2): string {
  const numberValue = Number(value);
  return Number.isFinite(numberValue) ? numberValue.toFixed(decimals) : "??";
}

function formatPercentOrUnknown(value: unknown, decimals = 1): string {
  const numberValue = Number(value);
  return Number.isFinite(numberValue) ? `${numberValue.toFixed(decimals)}%` : "??";
}

const baseColumns: GridColDef[] = [
  {
    field: "lineName",
    headerName: "Line Name",
    minWidth: 165,
    width: 185,
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
    headerName: "Time in Status",
    width: 140,
  },
  {
    field: "totalLength",
    headerName: "Total Length in Status",
    width: 180,
    valueFormatter: (value) => {
      const numberValue = Number(value);
      return Number.isFinite(numberValue) ? `${numberValue.toLocaleString()} ft` : "??";
    },
  },
  {
    field: "controlMode",
    headerName: "Control Mode",
    width: 120,
  },
  {
    field: "percentAutoMode",
    headerName: "% Auto",
    width: 115,
    valueFormatter: (value) => formatPercentOrUnknown(value, 1),
  },
  {
    field: "autoVariance",
    headerName: "Var in Auto Mode",
    width: 145,
    valueFormatter: (value) => formatNumberOrUnknown(value, 2),
  },
  {
    field: "percentManualMode",
    headerName: "% Manual",
    width: 115,
    valueFormatter: (value) => formatPercentOrUnknown(value, 1),
  },
  {
    field: "manualVariance",
    headerName: "% Manual Varience",
    width: 140,
    valueFormatter: (value) => formatNumberOrUnknown(value, 2),
  },
  {
    field: "totalVariance",
    headerName: "Total Varience",
    width: 125,
    valueFormatter: (value) => formatNumberOrUnknown(value, 2),
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
  const tableHeaderTextColor =
    getContrastRatio(appearance.tableColor, "#FFFFFF") >=
    getContrastRatio(appearance.tableColor, "#0F172A")
      ? "#FFFFFF"
      : "#0F172A";

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
            color: tableHeaderTextColor,
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