import { DataGrid } from "@mui/x-data-grid";

const columns = [
  { field: "line", headerName: "Line", width: 120 },
  { field: "product", headerName: "Product", width: 220 },
  { field: "status", headerName: "Status", width: 140 },
  { field: "mode", headerName: "Mode", width: 120 },
  { field: "length", headerName: "Length (ft)", width: 140 },
];

const rows = [
  {
    id: 1,
    line: "Line 1",
    product: "PVC Pipe",
    status: "Running",
    mode: "Auto",
    length: 15200,
  },
  {
    id: 2,
    line: "Line 2",
    product: "ABS Pipe",
    status: "Stopped",
    mode: "Manual",
    length: 9840,
  },
  {
    id: 3,
    line: "Line 3",
    product: "PEX Tubing",
    status: "Faulted",
    mode: "Auto",
    length: 12350,
  },
];

export default function LineTable() {
  return (
    <div style={{ height: 520, width: "100%" }}>
      <DataGrid
        rows={rows}
        columns={columns}
        pageSizeOptions={[10, 25, 50]}
      />
    </div>
  );
}