import { Drawer, List, ListItemButton, ListItemText } from "@mui/material";

const drawerWidth = 220;

export default function Sidebar() {
  return (
    <Drawer
      variant="permanent"
      sx={{
        width: drawerWidth,
        "& .MuiDrawer-paper": {
          width: drawerWidth,
          boxSizing: "border-box",
        },
      }}
    >
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
          <ListItemText primary="Settings" />
        </ListItemButton>
      </List>
    </Drawer>
  );
}