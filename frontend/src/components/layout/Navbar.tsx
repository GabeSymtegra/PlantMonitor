import {
  AppBar,
  Toolbar,
  Typography,
  Box,
  Chip,
  Button,
  IconButton,
  Tooltip,
} from "@mui/material";

import LogoutIcon from "@mui/icons-material/Logout";
import LoginIcon from "@mui/icons-material/Login";
import DarkModeIcon from "@mui/icons-material/DarkMode";
import LightModeIcon from "@mui/icons-material/LightMode";

import logo from "../../assets/logo.png";



import { useLocation, useNavigate } from "react-router-dom";

import { useAuth } from "../../context/useAuth";
import { useDashboard } from "../../context/useDashboard";
import { useThemeMode } from "../../context/useThemeMode";
import { LineStatus } from "../../types/LineStatus";
import { Rectangle } from "@mui/icons-material";

const statusColors: Record<LineStatus, string> = {
  [LineStatus.Running]: "#2E7D32",
  [LineStatus.Stopped]: "#ED6C02",
  [LineStatus.Bleedout]: "#0F766E",
  [LineStatus.Startup]: "#7C3AED",
  [LineStatus.Faulted]: "#D32F2F",
  [LineStatus.Offline]: "#616161",
  [LineStatus.Maintenance]: "#1565C0",
};

const statusOrder: LineStatus[] = [
  LineStatus.Running,
  LineStatus.Stopped,
  LineStatus.Bleedout,
  LineStatus.Startup,
  LineStatus.Faulted,
  LineStatus.Offline,
  LineStatus.Maintenance,
];

export default function Navbar() {
  const { isAuthenticated, user, logout } = useAuth();
  const { dashboard } = useDashboard();
  const { isDarkMode, toggleMode, appearance } = useThemeMode();
  const location = useLocation();
  const navigate = useNavigate();

  const monitorName = appearance.monitorName.trim() || "Plant";

  function getHeaderTitle(pathname: string): string {
    if (pathname === "/") {
      return `${monitorName} Monitor Dashboard`;
    }

    if (pathname.startsWith("/administration")) {
      return `${monitorName} Monitor Administration`;
    }

    if (pathname.startsWith("/settings")) {
      return `${monitorName} Monitor Settings`;
    }

    if (pathname.startsWith("/status-board")) {
      return `${monitorName} Monitor Status Board`;
    }

    if (pathname.startsWith("/lines/")) {
      return `${monitorName} Monitor Line Details`;
    }

    if (pathname.startsWith("/lines")) {
      return `${monitorName} Monitor Production Lines`;
    }

    if (pathname.startsWith("/products")) {
      return `${monitorName} Monitor Products`;
    }

    if (pathname.startsWith("/reports")) {
      return `${monitorName} Monitor Completed Runs`;
    }

    if (pathname.startsWith("/login")) {
      return `${monitorName} Monitor Login`;
    }

    return `${monitorName} Monitor`;
  }

  const headerTitle = getHeaderTitle(location.pathname);

  const lines = dashboard?.lines ?? [];
  const statusCounts = lines.reduce(
    (counts, line) => {
      counts[line.status] += 1;
      return counts;
    },
    {
      [LineStatus.Running]: 0,
      [LineStatus.Stopped]: 0,
      [LineStatus.Bleedout]: 0,
      [LineStatus.Startup]: 0,
      [LineStatus.Faulted]: 0,
      [LineStatus.Offline]: 0,
      [LineStatus.Maintenance]: 0,
    }
  );

  const updatedValue = dashboard?.lastUpdated
    ? dashboard.lastUpdated.toLocaleTimeString([], {
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
      })
    : "--:--:--";

  function handleAuthClick() {
    if (isAuthenticated) {
      logout();
      navigate("/");
      return;
    }

    navigate("/login");
  }

  return (
    <AppBar position="sticky" elevation={1}>
      <Toolbar
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr auto",
            lg: "auto 1fr auto",
          },
          alignItems: "center",
          columnGap: 2,
          rowGap: 1,
          minHeight: { xs: 88, lg: 72 },
        }}
      >
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 1,
            minWidth: 0,
          }}
        >
          <Rectangle
            component="img"
            src={logo}
            alt="Logo"
            sx={{
              height: 32,
              width: 256,
              flexShrink: 0,
              objectFit: "contain",
            }}
          />

          <Typography
            variant="h6"
            sx={{
              fontWeight: 700,
              letterSpacing: 1,
              minWidth: 0,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
            }}
          >
            {headerTitle}
          </Typography>
        </Box>

        <Box
          sx={{
            gridColumn: { xs: "1 / -1", lg: "auto" },
            order: { xs: 3, lg: 0 },
            display: "flex",
            justifyContent: { xs: "flex-start", lg: "center" },
            alignItems: "center",
            gap: 0.75,
            overflowX: "auto",
            whiteSpace: "nowrap",
            px: { xs: 0, lg: 1.5 },
            borderLeft: (theme) => `1px solid ${theme.palette.divider}`,
            borderRight: (theme) => `1px solid ${theme.palette.divider}`,
            "&::-webkit-scrollbar": {
              height: 4,
            },
          }}
        >
          {statusOrder.map((status) => (
            <Button
              key={status}
              size="small"
              onClick={() =>
                navigate(`/lines?status=${encodeURIComponent(status)}`)
              }
              sx={{
                minWidth: "auto",
                px: 1.2,
                py: 0.35,
                lineHeight: 1,
                color: "#fff",
                backgroundColor: statusColors[status],
                borderRadius: 2,
                fontSize: 12,
                fontWeight: 700,
                textTransform: "none",
                "&:hover": {
                  backgroundColor: statusColors[status],
                  filter: "brightness(0.92)",
                },
              }}
            >
              {status} {statusCounts[status]}
            </Button>
          ))}

          <Chip
            size="small"
            label={`Updated ${updatedValue}`}
            sx={{
              bgcolor: (theme) =>
                theme.palette.mode === "dark"
                  ? "rgba(255,255,255,0.12)"
                  : "rgba(0,0,0,0.12)",
              color: "inherit",
            }}
          />
        </Box>

        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            gap: { xs: 1, md: 1.75 },
            pl: { xs: 0, lg: 1 },
            whiteSpace: "nowrap",
            minWidth: 0,
          }}
        >
          <Tooltip title={isDarkMode ? "Switch to light mode" : "Switch to dark mode"}>
            <IconButton color="inherit" size="small" onClick={toggleMode}>
              {isDarkMode ? <LightModeIcon /> : <DarkModeIcon />}
            </IconButton>
          </Tooltip>

          <Chip color="success" label="LIVE" />

          <Typography sx={{ display: { xs: "none", md: "block" } }}>
            {isAuthenticated ? user?.username : "Guest"}
          </Typography>

          <Button
            color="inherit"
            onClick={handleAuthClick}
            startIcon={isAuthenticated ? <LogoutIcon /> : <LoginIcon />}
          >
            {isAuthenticated ? "Logout" : "Login"}
          </Button>
        </Box>
      </Toolbar>
    </AppBar>
  );
}