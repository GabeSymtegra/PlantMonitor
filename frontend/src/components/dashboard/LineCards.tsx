import {
  Card,
  CardContent,
  Divider,
  Typography,
  Box,
} from "@mui/material";

import StatusChip from "../common/StatusChip";

import { useDashboard } from "../../context/useDashboard";
import type { ProductionLine } from "../../types/ProductionLine";

function MetricRow({
  label,
  value,
}: {
  label: string;
  value: string;
}) {
  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: "max-content 1fr",
        columnGap: 1,
        alignItems: "baseline",
      }}
    >
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="body2" sx={{ fontWeight: 500 }}>
        {value}
      </Typography>
    </Box>
  );
}

interface LineCardsProps {
  lines?: ProductionLine[];
}

export default function LineCards({ lines }: LineCardsProps) {
  const { dashboard } = useDashboard();
  const visibleLines = lines ?? dashboard?.lines ?? [];

  function formatStartDateTime(value: string): string {
    const parsed = new Date(value);

    if (Number.isNaN(parsed.getTime())) {
      return value;
    }

    return parsed.toLocaleString();
  }

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
      {visibleLines.map((line) => (
        <Card key={line.id}>
          <CardContent>
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
              Line #{line.lineNumber}
            </Typography>

            <Typography gutterBottom sx={{ mb: 2 }}>
              {line.product}
            </Typography>

            <Box sx={{ display: "grid", rowGap: 0.75, mb: 2 }}>
              <Typography
                variant="overline"
                sx={{ color: "text.secondary", letterSpacing: 0.8 }}
              >
                Identity
              </Typography>

              <MetricRow
                label="Start Date:Time"
                value={formatStartDateTime(line.startDateTime)}
              />
            </Box>

            <Divider sx={{ my: 1.5 }} />

            <Box sx={{ display: "grid", rowGap: 0.75, mb: 2 }}>
              <Typography
                variant="overline"
                sx={{ color: "text.secondary", letterSpacing: 0.8 }}
              >
                Status
              </Typography>

              <Box
                sx={{
                  display: "grid",
                  gridTemplateColumns: "max-content 1fr",
                  columnGap: 1,
                  alignItems: "center",
                }}
              >
                <Typography variant="body2" color="text.secondary">
                  Status
                </Typography>
                <Box>
                  <StatusChip status={line.status} />
                </Box>
              </Box>

              <MetricRow label="Time" value={line.timeInStatus} />
              <MetricRow
                label="Total Length"
                value={`${line.totalLength.toLocaleString()} ft`}
              />
              <MetricRow label="Control Mode" value={line.controlMode} />
            </Box>

            <Divider sx={{ my: 1.5 }} />

            <Box sx={{ display: "grid", rowGap: 0.75 }}>
              <Typography
                variant="overline"
                sx={{ color: "text.secondary", letterSpacing: 0.8 }}
              >
                Variance
              </Typography>

              <MetricRow
                label="%Auto Mode"
                value={`${line.percentAutoMode.toFixed(1)}%`}
              />
              <MetricRow
                label="Auto Variance"
                value={line.autoVariance.toFixed(2)}
              />
              <MetricRow
                label="%Man Mode"
                value={`${line.percentManualMode.toFixed(1)}%`}
              />
              <MetricRow
                label="Man Variance"
                value={line.manualVariance.toFixed(2)}
              />
              <MetricRow
                label="Total Variance"
                value={line.totalVariance.toFixed(2)}
              />
            </Box>
          </CardContent>
        </Card>
      ))}
    </Box>
  );
}