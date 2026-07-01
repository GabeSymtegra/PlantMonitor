import { useEffect, useState } from "react";

import { DataGrid, type GridColDef } from "@mui/x-data-grid";

import StatusChip from "../common/StatusChip";

import { getProductionLines } from "../../services/dashboardService";

import type { ProductionLine } from "../../types/ProductionLine";
import type { LineStatus } from "../../types/LineStatus";

const columns: GridColDef[] = [
  {
    field: "lineNumber",
    headerName: "Line",
    width: 90,
  },
  {
    field: "product",
    headerName: "Product",
    flex: 1,
    minWidth: 220,
  },
  {
    field: "status",
    headerName: "Status",
    width: 170,

    renderCell: (params) => (
      <StatusChip status={params.value as LineStatus} />
    ),
  },
  {
    field: "controlMode",
    headerName: "Mode",
    width: 120,
  },
  {
    field: "totalLength",
    headerName: "Length (ft)",
    width: 140,
  },
  {
    field: "runtime",
    headerName: "Runtime",
    width: 140,
  },
  {
    field: "plcIp",
    headerName: "PLC IP",
    width: 150,
  },
];

export default function LineTable() {
  const [rows, setRows] = useState<ProductionLine[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function loadLines() {
      const data = await getProductionLines();

      setRows(data);

      setLoading(false);
    }

    loadLines();
  }, []);

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
        pageSizeOptions={[10, 25, 50]}
        disableRowSelectionOnClick
      />
    </div>
  );
}