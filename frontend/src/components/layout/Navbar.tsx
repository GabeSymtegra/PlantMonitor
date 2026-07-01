import {
  AppBar,
  Toolbar,
  Typography,
  Box,
  Chip,
  IconButton,
  Button,
} from "@mui/material";

import MenuIcon from "@mui/icons-material/Menu";
import MenuOpenIcon from "@mui/icons-material/MenuOpen";
import LogoutIcon from "@mui/icons-material/Logout";
import LoginIcon from "@mui/icons-material/Login";
import { useNavigate } from "react-router-dom";

import { useAuth } from "../../context/useAuth";

interface NavbarProps {
  sidebarOpen: boolean;
  onToggleSidebar: () => void;
}

export default function Navbar({ sidebarOpen, onToggleSidebar }: NavbarProps) {
  const { isAuthenticated, user, logout } = useAuth();
  const navigate = useNavigate();

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
      <Toolbar>
        <IconButton
          color="inherit"
          onClick={onToggleSidebar}
          sx={{ mr: 2 }}
          aria-label={sidebarOpen ? "Collapse sidebar" : "Expand sidebar"}
        >
          {sidebarOpen ? <MenuOpenIcon /> : <MenuIcon />}
        </IconButton>

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
          {isAuthenticated ? user?.username : "Guest"}
        </Typography>

        <Button
          color="inherit"
          onClick={handleAuthClick}
          startIcon={isAuthenticated ? <LogoutIcon /> : <LoginIcon />}
        >
          {isAuthenticated ? "Logout" : "Login"}
        </Button>
      </Toolbar>
    </AppBar>
  );
}