import {
  Alert,
  Box,
  Button,
  Paper,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";

import { useAuth } from "../context/useAuth";

export default function Login() {
  const [username, setUsername] = useState("admin");
  const [password, setPassword] = useState("admin");
  const [error, setError] = useState("");

  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const redirectTo = (location.state as { from?: string } | null)?.from ?? "/";

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");

    const authenticated = login(username.trim(), password);

    if (!authenticated) {
      setError("Invalid credentials. Use admin / admin for now.");
      return;
    }

    navigate(redirectTo, { replace: true });
  }

  return (
    <Box
      sx={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        p: 3,
      }}
    >
      <Paper sx={{ width: "100%", maxWidth: 420, p: 4 }} elevation={3}>
        <Stack spacing={2.5} component="form" onSubmit={handleSubmit}>
          <Typography variant="h4" sx={{ fontWeight: 700 }}>
            Admin Login
          </Typography>

          <Typography color="text.secondary">
            Use admin/admin credentials.
          </Typography>

          {error ? <Alert severity="error">{error}</Alert> : null}

          <TextField
            label="Username"
            value={username}
            onChange={(event) => setUsername(event.target.value)}
            fullWidth
            autoFocus
          />

          <TextField
            label="Password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            type="password"
            fullWidth
          />

          <Button type="submit" variant="contained" size="large">
            Sign In
          </Button>
        </Stack>
      </Paper>
    </Box>
  );
}