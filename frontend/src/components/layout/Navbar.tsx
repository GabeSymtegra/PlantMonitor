import {
  AppBar,
  Toolbar,
  Typography,
  Box,
  Chip,
  IconButton,
} from "@mui/material";

import SettingsIcon from "@mui/icons-material/Settings";

export default function Navbar() {
  return (
    <AppBar position="sticky" elevation={1}>
      <Toolbar>
        <Typography
          variant="h5"
          sx={{
            fontWeight: 700,
            letterSpacing: 1,
          }}
        >
          🌿 PlantMonitor
        </Typography>

        <Box sx={{ flexGrow: 1 }} />

        <Chip color="success" label="LIVE" sx={{ mr: 3 }} />

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