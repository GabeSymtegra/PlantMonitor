import {
  AppBar,
  Toolbar,
  Typography,
  Box,
  IconButton,
} from "@mui/material";

import SettingsIcon from "@mui/icons-material/Settings";

export default function Navbar() {
  return (
    <AppBar position="sticky">
      <Toolbar>

        <Typography variant="h6">
          🌿 PlantMonitor
        </Typography>

        <Box sx={{ flexGrow: 1 }} />

        <Typography sx={{ mr: 2 }}>
          Gabriel
        </Typography>

        <IconButton color="inherit">
          <SettingsIcon />
        </IconButton>

      </Toolbar>
    </AppBar>
  );
}