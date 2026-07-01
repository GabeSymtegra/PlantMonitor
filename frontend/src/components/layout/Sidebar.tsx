import {
  Drawer,
  Toolbar,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
} from "@mui/material";

import DashboardIcon from "@mui/icons-material/Dashboard";
import Inventory2Icon from "@mui/icons-material/Inventory2";
import AssessmentIcon from "@mui/icons-material/Assessment";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";
import SettingsIcon from "@mui/icons-material/Settings";

import { NavLink } from "react-router-dom";

import { useAuth } from "../../context/useAuth";

const drawerWidth = 220;
const collapsedDrawerWidth = 60;

interface SidebarProps {
  open: boolean;
}

export default function Sidebar({ open }: SidebarProps) {
  const currentWidth = open ? drawerWidth : collapsedDrawerWidth;
  const { isAuthenticated } = useAuth();

  const iconSx = {
    minWidth: 0,
    justifyContent: "center",
    width: 34,
    color: "text.secondary",
    "& .MuiSvgIcon-root": {
      fontSize: 20,
    },
  };

  const textSx = {
    opacity: open ? 1 : 0,
    maxWidth: open ? 150 : 0,
    whiteSpace: "nowrap",
    "& .MuiListItemText-primary": {
      fontSize: 14,
      fontWeight: 500,
      color: "text.secondary",
    },
    transition: (theme) =>
      theme.transitions.create(["opacity", "max-width"], {
        duration: theme.transitions.duration.shorter,
      }),
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
        width: currentWidth,
        flexShrink: 0,
        transition: (theme) =>
          theme.transitions.create("width", {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.standard,
          }),
        overflowX: "hidden",

        "& .MuiDrawer-paper": {
          width: currentWidth,
          boxSizing: "border-box",
          overflowX: "hidden",
          borderRight: "1px solid rgba(0, 0, 0, 0.08)",
          backgroundColor: "rgba(255, 255, 255, 0.92)",
          transition: (theme) =>
            theme.transitions.create(["width", "border-right"], {
              easing: theme.transitions.easing.sharp,
              duration: theme.transitions.duration.standard,
            }),
        },
      }}
    >
      <Toolbar />

      <List sx={{ pt: 1 }}>
        <ListItemButton component={NavLink} to="/" sx={itemSx}>
          <ListItemIcon sx={iconSx}>
            <DashboardIcon />
          </ListItemIcon>

          <ListItemText primary="Dashboard" sx={textSx} />
        </ListItemButton>

        <ListItemButton component={NavLink} to="/products" sx={itemSx}>
          <ListItemIcon sx={iconSx}>
            <Inventory2Icon />
          </ListItemIcon>

          <ListItemText primary="Products" sx={textSx} />
        </ListItemButton>

        <ListItemButton component={NavLink} to="/reports" sx={itemSx}>
          <ListItemIcon sx={iconSx}>
            <AssessmentIcon />
          </ListItemIcon>

          <ListItemText primary="Reports" sx={textSx} />
        </ListItemButton>

        {isAuthenticated ? (
          <ListItemButton component={NavLink} to="/administration" sx={itemSx}>
            <ListItemIcon sx={iconSx}>
              <AdminPanelSettingsIcon />
            </ListItemIcon>

            <ListItemText primary="Administration" sx={textSx} />
          </ListItemButton>
        ) : null}

        <ListItemButton component={NavLink} to="/settings" sx={itemSx}>
          <ListItemIcon sx={iconSx}>
            <SettingsIcon />
          </ListItemIcon>

          <ListItemText primary="Settings" sx={textSx} />
        </ListItemButton>
      </List>
    </Drawer>
  );
}