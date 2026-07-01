import {
  Drawer,
  Toolbar,
  List,
  ListItemButton,
  ListItemText,
} from "@mui/material";

const drawerWidth = 240;

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
        },
      }}
    >
      <Toolbar />

      <List>

        <ListItemButton>
          <ListItemText primary="Dashboard" />
        </ListItemButton>

        <ListItemButton>
          <ListItemText primary="Production Lines" />
        </ListItemButton>

        <ListItemButton>
          <ListItemText primary="Products" />
        </ListItemButton>

        <ListItemButton>
          <ListItemText primary="Reports" />
        </ListItemButton>

        <ListItemButton>
          <ListItemText primary="Administration" />
        </ListItemButton>

        <ListItemButton>
          <ListItemText primary="Settings" />
        </ListItemButton>

      </List>
    </Drawer>
  );
}