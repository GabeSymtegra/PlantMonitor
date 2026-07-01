import { Card, CardContent, Typography } from "@mui/material";

export default function LineCard() {
  return (
    <Card sx={{ width: 300 }}>
      <CardContent>

        <Typography variant="h5">
          Line 1
        </Typography>

        <Typography>
          Product: Pepsi
        </Typography>

        <Typography>
          Status: Running
        </Typography>

        <Typography>
          Mode: Auto
        </Typography>

        <Typography>
          Length: 14,220 ft
        </Typography>

      </CardContent>
    </Card>
  );
}