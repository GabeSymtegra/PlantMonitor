import {
  Drawer,
  Toolbar,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
} from "@mui/material";

import DashboardIcon from "@mui/icons-material/Dashboard";
import PrecisionManufacturingIcon from "@mui/icons-material/PrecisionManufacturing";
import Inventory2Icon from "@mui/icons-material/Inventory2";
import AssessmentIcon from "@mui/icons-material/Assessment";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";
import SettingsIcon from "@mui/icons-material/Settings";

const drawerWidth = 250;

export default function Sidebar() {
  return (
    <Drawer
      variant="permanent"
      sx={{
        width: drawerWidth,
        flexShrink: 0,
        "& .MuiDrawer-paper": {
          width: drawerWidth,
          boxSizing: "border-box",
          borderRight: "1px solid #ddd",
        },
      }}
    >
      <Toolbar />

      <List>

        <ListItemButton selected>

          <ListItemIcon>
            <DashboardIcon />
          </ListItemIcon>

          <ListItemText primary="Dashboard" />

        </ListItemButton>

        <ListItemButton>

          <ListItemIcon>
            <PrecisionManufacturingIcon />
          </ListItemIcon>

          <ListItemText primary="Production Lines" />

        </ListItemButton>

        <ListItemButton>

          <ListItemIcon>
            <Inventory2Icon />
          </ListItemIcon>

          <ListItemText primary="Products" />

        </ListItemButton>

        <ListItemButton>

          <ListItemIcon>
            <AssessmentIcon />
          </ListItemIcon>

          <ListItemText primary="Reports" />

        </ListItemButton>

        <ListItemButton>

          <ListItemIcon>
            <AdminPanelSettingsIcon />
          </ListItemIcon>

          <ListItemText primary="Administration" />

        </ListItemButton>

        <ListItemButton>

          <ListItemIcon>
            <SettingsIcon />
          </ListItemIcon>

          <ListItemText primary="Settings" />

        </ListItemButton>

      </List>
    </Drawer>
  );
}