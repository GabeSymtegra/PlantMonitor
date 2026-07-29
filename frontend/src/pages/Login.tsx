import {
  Alert,
  Box,
  Button,
  FormControlLabel,
  Paper,
  Checkbox,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";

import { useAuth } from "../context/useAuth";

export default function Login() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(true);
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const { login, authError } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const redirectTo = (location.state as { from?: string } | null)?.from ?? "/";

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setSubmitting(true);

    const authenticated = await login(username.trim(), password, rememberMe);

    setSubmitting(false);

    if (!authenticated) {
      setError(authError ?? "Invalid credentials.");
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
            Plant Monitor Login
          </Typography>

          <Typography color="text.secondary">
            Sign in to access your authorized Plant Monitor pages.
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

          <FormControlLabel
            control={
              <Checkbox
                checked={rememberMe}
                onChange={(event) => setRememberMe(event.target.checked)}
              />
            }
            label="Remember me"
          />

          <Button
            type="submit"
            variant="contained"
            size="large"
            disabled={submitting}
          >
            Sign In
          </Button>
        </Stack>
      </Paper>
    </Box>
  );
}