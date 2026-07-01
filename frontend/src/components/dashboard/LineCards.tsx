import {
  Card,
  CardContent,
  Typography,
  Box,
} from "@mui/material";

import StatusChip from "../common/StatusChip";

import { useDashboard } from "../../context/useDashboard";

export default function LineCards() {
  const { dashboard } = useDashboard();

  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: {
          xs: "1fr",
          md: "repeat(2, 1fr)",
          lg: "repeat(3, 1fr)",
        },
        gap: 2,
      }}
    >
      {dashboard?.lines.map((line) => (
        <Card key={line.id}>
          <CardContent>
            <Typography variant="h6">
              Line {line.lineNumber}
            </Typography>

            <Typography gutterBottom>
              {line.product}
            </Typography>

            <StatusChip status={line.status} />

            <Typography sx={{ mt: 2 }}>
              Mode: {line.controlMode}
            </Typography>

            <Typography>
              Length: {line.totalLength.toLocaleString()} ft
            </Typography>

            <Typography>
              Runtime: {line.runtime}
            </Typography>

            <Typography>
              PLC: {line.plcIp}
            </Typography>
          </CardContent>
        </Card>
      ))}
    </Box>
  );
}