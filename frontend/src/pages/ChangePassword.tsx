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
import { apiPost, ApiRequestError } from "../services/api/client";

export default function ChangePassword() {
  const { user, refreshSession } = useAuth();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();

  const from = (location.state as { from?: string } | null)?.from ?? "/";

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setSuccess("");

    if (newPassword.length < 12) {
      setError("New password must be at least 12 characters long.");
      return;
    }

    if (newPassword !== confirmPassword) {
      setError("New password and confirmation do not match.");
      return;
    }

    setSubmitting(true);

    try {
      await apiPost<void, { currentPassword: string; newPassword: string }>(
        "/auth/change-password",
        {
          currentPassword,
          newPassword,
        }
      );

      await refreshSession();
      setSuccess("Password updated successfully.");
      navigate(from, { replace: true });
    } catch (requestError) {
      const message =
        requestError instanceof ApiRequestError
          ? requestError.status === 403
            ? "Your session requires a password change before continuing."
            : requestError.message
          : requestError instanceof Error
            ? requestError.message
            : "Password change failed.";

      setError(message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Box sx={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", p: 3 }}>
      <Paper sx={{ width: "100%", maxWidth: 520, p: 4 }} elevation={3}>
        <Stack spacing={2.5} component="form" onSubmit={handleSubmit}>
          <Typography variant="h4" sx={{ fontWeight: 700 }}>
            Change Password
          </Typography>

          <Typography color="text.secondary">
            {user?.mustChangePassword
              ? "Your administrator requires a password change before you can continue."
              : "Update your password to keep using the application."}
          </Typography>

          {error ? <Alert severity="error">{error}</Alert> : null}
          {success ? <Alert severity="success">{success}</Alert> : null}

          <TextField
            label="Current Password"
            type="password"
            value={currentPassword}
            onChange={(event) => setCurrentPassword(event.target.value)}
            fullWidth
          />

          <TextField
            label="New Password"
            type="password"
            value={newPassword}
            onChange={(event) => setNewPassword(event.target.value)}
            fullWidth
            helperText="Must be at least 12 characters long."
          />

          <TextField
            label="Confirm New Password"
            type="password"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            fullWidth
          />

          <Stack direction="row" spacing={1.5} justifyContent="flex-end">
            <Button variant="outlined" onClick={() => navigate(from, { replace: true })}>
              Cancel
            </Button>
            <Button type="submit" variant="contained" disabled={submitting}>
              Save Password
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </Box>
  );
}
