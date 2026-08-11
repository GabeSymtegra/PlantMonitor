import {
  Card,
  CardContent,
  Divider,
  Typography,
  Box,
} from "@mui/material";
import { useNavigate } from "react-router-dom";

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

function formatNumberOrUnknown(value: unknown, decimals = 2): string {
  const numberValue = Number(value);
  return Number.isFinite(numberValue) ? numberValue.toFixed(decimals) : "??";
}

function formatPercentOrUnknown(value: unknown, decimals = 1): string {
  const numberValue = Number(value);
  return Number.isFinite(numberValue) ? `${numberValue.toFixed(decimals)}%` : "??";
}

export default function LineCards({ lines }: LineCardsProps) {
  const { dashboard } = useDashboard();
  const visibleLines = lines ?? dashboard?.lines ?? [];
  const navigate = useNavigate();

  function displayOrUnknown(value: string | null | undefined): string {
    const trimmed = value?.trim();
    return trimmed && trimmed.length > 0 ? trimmed : "Unknown";
  }

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
        <Card
          key={line.id}
          onClick={() => navigate(`/lines/${line.id}`)}
          sx={{
            cursor: "pointer",
            transition: "transform 120ms ease, box-shadow 120ms ease",
            "&:hover": {
              transform: "translateY(-2px)",
              boxShadow: 5,
            },
          }}
        >
          <CardContent>
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
              {line.lineName}
            </Typography>

            <Box sx={{ mb: 2 }} />

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
              <MetricRow label="Product" value={displayOrUnknown(line.product)} />
              <MetricRow label="Recipe" value={displayOrUnknown(line.recipeId)} />
              <MetricRow label="Machine" value={displayOrUnknown(line.machineId)} />
              <MetricRow label="Operator" value={displayOrUnknown(line.operatorName)} />
              <MetricRow label="PLC IP" value={displayOrUnknown(line.plcIp)} />
              <MetricRow label="Manufacturer" value={displayOrUnknown(line.manufacturer)} />
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

              <MetricRow label="Time in Status" value={line.timeInStatus} />
              <MetricRow
                label="Total Length in Status"
                value={Number.isFinite(Number(line.totalLength)) ? `${line.totalLength.toLocaleString()} ft` : "??"}
              />
              <MetricRow label="Control Mode" value={line.controlMode} />
            </Box>

            <Divider sx={{ my: 1.5 }} />

            <Box sx={{ display: "grid", rowGap: 0.75 }}>
              <Typography
                variant="overline"
                sx={{ color: "text.secondary", letterSpacing: 0.8 }}
              >
                Quality
              </Typography>

              <MetricRow
                label="% Auto"
                value={formatPercentOrUnknown(line.percentAutoMode, 1)}
              />
              <MetricRow
                label="Var in Auto Mode"
                value={formatNumberOrUnknown(line.autoVariance, 2)}
              />
              <MetricRow
                label="% Manual"
                value={formatPercentOrUnknown(line.percentManualMode, 1)}
              />
              <MetricRow
                label="% Manual Varience"
                value={formatNumberOrUnknown(line.manualVariance, 2)}
              />
              <MetricRow
                label="Total Varience"
                value={formatNumberOrUnknown(line.totalVariance, 2)}
              />
            </Box>
          </CardContent>
        </Card>
      ))}
    </Box>
  );
}