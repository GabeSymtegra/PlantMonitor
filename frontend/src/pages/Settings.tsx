import {
  Button,
  Chip,
  FormControl,
  FormControlLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Switch,
  TextField,
  Typography,
} from "@mui/material";
import { getContrastRatio } from "@mui/material/styles";
import LaunchIcon from "@mui/icons-material/Launch";
import { useNavigate } from "react-router-dom";

import {
  type AppearanceSettings,
  type TextSize,
} from "../context/ThemeContext";
import { useThemeMode } from "../context/useThemeMode";

const textSizeOptions: Array<{ value: TextSize; label: string }> = [
  { value: "small", label: "Small" },
  { value: "medium", label: "Medium" },
  { value: "large", label: "Large" },
];

function getReadableTextColor(background: string) {
  const whiteContrast = getContrastRatio(background, "#FFFFFF");
  const darkContrast = getContrastRatio(background, "#0F172A");

  return whiteContrast >= darkContrast ? "#FFFFFF" : "#0F172A";
}

const themePresets: Array<{
  id: string;
  label: string;
  description: string;
  colors: Pick<AppearanceSettings, "headerColor" | "backgroundColor" | "tableColor">;
}> = [
  {
    id: "classic",
    label: "Classic Plant",
    description: "Balanced blue with bright table contrast",
    colors: {
      headerColor: "#1565C0",
      backgroundColor: "#F4F6F8",
      tableColor: "#E3F2FD",
    },
  },
  {
    id: "spec-ops",
    label: "Spec Ops",
    description: "Low-glare dark tactical display",
    colors: {
      headerColor: "#0B1F35",
      backgroundColor: "#121820",
      tableColor: "#223247",
    },
  },
  {
    id: "high-visibility",
    label: "High Visibility",
    description: "Strong contrast for long-distance viewing",
    colors: {
      headerColor: "#A61B1B",
      backgroundColor: "#F5F0E8",
      tableColor: "#FFE8C2",
    },
  },
  {
    id: "industrial-green",
    label: "Industrial Green",
    description: "Factory-style green and steel tones",
    colors: {
      headerColor: "#1D5C45",
      backgroundColor: "#E9EFEC",
      tableColor: "#D5E4DD",
    },
  },
];

export default function Settings() {
  const navigate = useNavigate();
  const {
    mode,
    isDarkMode,
    setMode,
    toggleMode,
    appearance,
    updateAppearance,
    resetAppearance,
  } = useThemeMode();

  return (
      <Stack spacing={2.5}>
        <Typography variant="h4" sx={{ fontWeight: 700 }}>
          Display Settings
        </Typography>

        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Typography variant="h6">Color Mode</Typography>

            <FormControlLabel
              control={<Switch checked={isDarkMode} onChange={toggleMode} />}
              label={isDarkMode ? "Dark Mode Enabled" : "Light Mode Enabled"}
            />

            <FormControl size="small" sx={{ maxWidth: 260 }}>
              <Select
                value={mode}
                onChange={(event) =>
                  setMode(event.target.value as "light" | "dark")
                }
              >
                <MenuItem value="light">Light</MenuItem>
                <MenuItem value="dark">Dark</MenuItem>
              </Select>
            </FormControl>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Typography variant="h6">Branding and Theme Presets</Typography>

            <TextField
              label="Monitor Name"
              size="small"
              value={appearance.monitorName}
              onChange={(event) =>
                updateAppearance({ monitorName: event.target.value })
              }
              helperText="Used in page titles, for example: Plant Monitor Dashboard"
              sx={{ maxWidth: 360 }}
            />

            <Typography color="text.secondary">
              Choose a preset for instant color changes without the color-picker lag.
            </Typography>

            <Stack spacing={1.2}>
              {themePresets.map((preset) => {
                const isActive =
                  appearance.headerColor.toLowerCase() === preset.colors.headerColor.toLowerCase() &&
                  appearance.backgroundColor.toLowerCase() === preset.colors.backgroundColor.toLowerCase() &&
                  appearance.tableColor.toLowerCase() === preset.colors.tableColor.toLowerCase();
                const headerChipText = getReadableTextColor(preset.colors.headerColor);
                const backgroundChipText = getReadableTextColor(preset.colors.backgroundColor);
                const tableChipText = getReadableTextColor(preset.colors.tableColor);

                return (
                  <Button
                    key={preset.id}
                    variant={isActive ? "contained" : "outlined"}
                    onClick={() => updateAppearance(preset.colors)}
                    sx={{
                      justifyContent: "space-between",
                      textTransform: "none",
                      py: 1,
                      px: 1.4,
                      ...(isActive && {
                        color: (theme) => theme.palette.primary.contrastText,
                      }),
                    }}
                  >
                    <Stack alignItems="flex-start" spacing={0.4}>
                      <Typography sx={{ fontWeight: 700 }}>{preset.label}</Typography>
                      <Typography variant="body2" color="text.secondary">
                        {preset.description}
                      </Typography>
                    </Stack>

                    <Stack direction="row" spacing={0.6}>
                      <Chip
                        label="H"
                        size="small"
                        sx={{
                          bgcolor: preset.colors.headerColor,
                          color: headerChipText,
                          fontWeight: 700,
                        }}
                      />
                      <Chip
                        label="B"
                        size="small"
                        sx={{
                          bgcolor: preset.colors.backgroundColor,
                          color: backgroundChipText,
                          fontWeight: 700,
                        }}
                      />
                      <Chip
                        label="T"
                        size="small"
                        sx={{
                          bgcolor: preset.colors.tableColor,
                          color: tableChipText,
                          fontWeight: 700,
                        }}
                      />
                    </Stack>
                  </Button>
                );
              })}
            </Stack>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Typography variant="h6">Text Readability</Typography>

            <FormControl size="small" sx={{ maxWidth: 220 }}>
              <Select
                value={appearance.textSize}
                onChange={(event) =>
                  updateAppearance({ textSize: event.target.value as TextSize })
                }
              >
                {textSizeOptions.map((option) => (
                  <MenuItem key={option.value} value={option.value}>
                    {option.label}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <FormControlLabel
              control={
                <Switch
                  checked={appearance.boldText}
                  onChange={(event) =>
                    updateAppearance({ boldText: event.target.checked })
                  }
                />
              }
              label="Use bolder text across the app"
            />
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Typography variant="h6">Factory Floor Board</Typography>
            <Typography color="text.secondary">
              Open the airplane departure-style status board for large-screen floor viewing.
            </Typography>

            <Button
              variant="contained"
              startIcon={<LaunchIcon />}
              onClick={() => navigate("/status-board")}
              sx={{ alignSelf: "flex-start" }}
            >
              Open Status Board
            </Button>
          </Stack>
        </Paper>

        <Stack direction="row" spacing={1.5}>
          <Button variant="outlined" onClick={resetAppearance}>
            Reset Appearance
          </Button>
        </Stack>
      </Stack>
  );
}