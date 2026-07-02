import { Button, Paper, Stack, Typography } from "@mui/material";
import { useLocation, useNavigate } from "react-router-dom";

import type { UserRole } from "../models/User";

interface ForbiddenState {
  from?: string;
  requiredRoles?: UserRole[];
}

export default function Forbidden() {
  const navigate = useNavigate();
  const location = useLocation();
  const state = (location.state as ForbiddenState | null) ?? null;

  const required = state?.requiredRoles?.length
    ? state.requiredRoles.join(", ")
    : "an authorized role";

  return (
    <Stack spacing={2.5} sx={{ maxWidth: 760 }}>
      <Typography variant="h4" sx={{ fontWeight: 700 }}>
        Access Denied
      </Typography>

      <Paper sx={{ p: 2.5 }}>
        <Stack spacing={1.5}>
          <Typography>
            You do not have permission to access this configuration area.
          </Typography>

          <Typography color="text.secondary">
            Required role: {required}
          </Typography>

          {state?.from ? (
            <Typography color="text.secondary">
              Requested route: {state.from}
            </Typography>
          ) : null}

          <Stack direction="row" spacing={1.25} sx={{ pt: 1 }}>
            <Button variant="contained" onClick={() => navigate("/")}>
              Go to Dashboard
            </Button>

            <Button variant="outlined" onClick={() => navigate(-1)}>
              Go Back
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </Stack>
  );
}
