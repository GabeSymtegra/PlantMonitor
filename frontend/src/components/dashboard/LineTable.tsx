import { DataGrid, type GridColDef } from "@mui/x-data-grid";

import StatusChip from "../common/StatusChip";

import { useDashboard } from "../../context/useDashboard";

import type { LineStatus } from "../../types/LineStatus";

function formatDateTime(value: string): string {
  const parsed = new Date(value);

  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString();
}

const columns: GridColDef[] = [
  {
    field: "lineNumber",
    headerName: "Line #",
    width: 85,
  },
  {
    field: "product",
    headerName: "Product",
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

export default function LineTable() {
  const { dashboard, loading } = useDashboard();

  return (
    <div
      style={{
        height: 550,
        width: "100%",
      }}
    >
      <DataGrid
        rows={dashboard?.lines ?? []}
        columns={columns}
        loading={loading}
        pageSizeOptions={[10, 25, 50]}
        initialState={{
          pagination: {
            paginationModel: {
              pageSize: 10,
              page: 0,
            },
          },
        }}
        disableRowSelectionOnClick
      />
    </div>
  );
}