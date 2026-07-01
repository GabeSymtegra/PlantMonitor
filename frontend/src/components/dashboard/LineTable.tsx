import { useEffect, useState } from "react";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";

import { getProductionLines } from "../../services/dashboardService";
import type { ProductionLine } from "../../types/ProductionLine";

const columns: GridColDef[] = [
  {
    field: "lineNumber",
    headerName: "Line",
    width: 100,
  },
  {
    field: "product",
    headerName: "Product",
    flex: 1,
    minWidth: 180,
  },
  {
    field: "status",
    headerName: "Status",
    width: 140,
  },
  {
    field: "controlMode",
    headerName: "Mode",
    width: 120,
  },
  {
    field: "totalLength",
    headerName: "Length (ft)",
    width: 150,
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
    <div style={{ height: 520, width: "100%" }}>
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