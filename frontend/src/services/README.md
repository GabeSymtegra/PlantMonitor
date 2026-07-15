# Frontend Services

Services provide the frontend data access layer.

## Current Files

- `api/`: fetch wrapper, bearer token handling, and error shaping
- `dashboardService.ts`: dashboard and line detail API transforms
- `plcConnectionService.ts`: PLC connection and admin connection helpers
- `plcTagBrowserService.ts`: PLC browse and read helpers used by administration flows
- `reportsService.ts`: completed run and runtime event report retrieval

Keep HTTP details here so pages and components stay focused on presentation.