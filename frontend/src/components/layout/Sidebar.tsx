import {
  Drawer,
  Toolbar,
  List,
  ListItemButton,
  ListItemIcon,
  Tooltip,
} from "@mui/material";

import DashboardIcon from "@mui/icons-material/Dashboard";
import Inventory2Icon from "@mui/icons-material/Inventory2";
import AssessmentIcon from "@mui/icons-material/Assessment";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";
import SettingsIcon from "@mui/icons-material/Settings";
import DepartureBoardIcon from "@mui/icons-material/DepartureBoard";

import { NavLink } from "react-router-dom";

import { useAuth } from "../../context/useAuth";

const drawerWidth = 64;

export default function Sidebar() {
  const { canConfigure } = useAuth();

  const iconSx = {
    minWidth: 0,
    justifyContent: "center",
    width: 34,
    color: "text.secondary",
    "& .MuiSvgIcon-root": {
      fontSize: 20,
    },
  };

  const itemSx = {
    mx: 0.75,
    my: 0.25,
    minHeight: 40,
    px: 1,
    borderRadius: 1.5,
  };

  return (
    <Drawer
      variant="permanent"
      sx={{
        width: drawerWidth,
        flexShrink: 0,
        overflowX: "hidden",

        "& .MuiDrawer-paper": {
          width: drawerWidth,
          boxSizing: "border-box",
          overflowX: "hidden",
          borderRight: (theme) => `1px solid ${theme.palette.divider}`,
          backgroundColor: (theme) =>
            theme.palette.mode === "dark"
              ? "rgba(23, 28, 34, 0.92)"
              : "rgba(255, 255, 255, 0.92)",
        },
      }}
    >
      <Toolbar />

      <List sx={{ pt: 1 }}>
        <Tooltip title="Dashboard" placement="right">
          <ListItemButton component={NavLink} to="/" sx={itemSx}>
            <ListItemIcon sx={iconSx}>
              <DashboardIcon />
            </ListItemIcon>
          </ListItemButton>
        </Tooltip>

        <Tooltip title="Products" placement="right">
          <ListItemButton component={NavLink} to="/products" sx={itemSx}>
            <ListItemIcon sx={iconSx}>
              <Inventory2Icon />
            </ListItemIcon>
          </ListItemButton>
        </Tooltip>

        <Tooltip title="Reports" placement="right">
          <ListItemButton component={NavLink} to="/reports" sx={itemSx}>
            <ListItemIcon sx={iconSx}>
              <AssessmentIcon />
            </ListItemIcon>
          </ListItemButton>
        </Tooltip>

        <Tooltip title="Status Board" placement="right">
          <ListItemButton component={NavLink} to="/status-board" sx={itemSx}>
            <ListItemIcon sx={iconSx}>
              <DepartureBoardIcon />
            </ListItemIcon>
          </ListItemButton>
        </Tooltip>

        {canConfigure ? (
          <Tooltip title="Administration" placement="right">
            <ListItemButton component={NavLink} to="/administration" sx={itemSx}>
              <ListItemIcon sx={iconSx}>
                <AdminPanelSettingsIcon />
              </ListItemIcon>
            </ListItemButton>
          </Tooltip>
        ) : null}

        {canConfigure ? (
          <Tooltip title="Settings" placement="right">
            <ListItemButton component={NavLink} to="/settings" sx={itemSx}>
              <ListItemIcon sx={iconSx}>
                <SettingsIcon />
              </ListItemIcon>
            </ListItemButton>
          </Tooltip>
        ) : null}
      </List>
    </Drawer>
  );
}