# PlantMonitor

PlantMonitor is a manufacturing line monitoring application with a React frontend and an ASP.NET Core backend. It supports authenticated administration, live PLC connectivity, line runtime tracking, status dashboards, and reporting for completed runs plus mode and status transitions.

## Startup Procedure

Use this sequence for real host startup, service verification, and wireless user access.

1. Boot the PlantMonitor host computer and connect it to the plant wired network.
2. Sign in to Windows with an account that can run elevated PowerShell commands.
3. Open PowerShell as Administrator and go to the repository root.
4. Publish the current host release payload:

```powershell
.\scripts\publish-host-release.ps1
```

5. Install or refresh the LAN-hosted services (backend + privileged agent):

```powershell
.\scripts\install-lan-service.ps1
```

6. Verify service health and hosted frontend availability:

```powershell
.\scripts\test-lan-service.ps1
```

7. Verify credential login flow with a seeded account:

```powershell
.\scripts\test-lan-service.ps1 -Username operator -Password test
```

8. From the host browser, open the Settings page and confirm Network Settings shows:
- Connectivity diagnostics
- Recommended dashboard URLs
- Wi-Fi status and scan results

9. Connect wireless client devices (tablets/laptops/phones) to plant Wi-Fi and open one of the recommended host URLs, for example:
- http://<host-ip>:5050

10. Sign in from wireless clients and validate role access:
- Admin: admin / test
- Operator: operator / test
- Viewer: viewer / test

11. Confirm live operation on wireless clients:
- Dashboard loads
- SignalR updates continue
- Status Board opens when needed

If any step fails, run [scripts/test-lan-service.ps1](scripts/test-lan-service.ps1) again and review [docs/Deployment.md](docs/Deployment.md) for remediation.

## Start Here

- User guide: [docs/User-Guide.md](docs/User-Guide.md)
- Backend structure guide: [backend/README.md](backend/README.md)
- Frontend structure guide: [frontend/README.md](frontend/README.md)
- Docs index: [docs/README.md](docs/README.md)
- Scripts guide: [scripts/README.md](scripts/README.md)

## Current Capabilities

- JWT authentication with Admin, Operator, and Viewer roles.
- Live dashboard data from the backend runtime service.
- PLC support for Allen-Bradley and Siemens drivers.
- Administration workflows for line configuration, protocol assignment, tag browsing, and validation.
- SQLite persistence for configuration, completed production runs, and runtime events.
- Reports for completed runs and mode/status switch history.
- SignalR refresh notifications for dashboard consumers.

## Repository Layout

```text
backend/         ASP.NET Core API, runtime engine, PLC services, EF Core persistence
backend.Tests/   Backend test project
frontend/        React application and frontend build/test tooling
docs/            User-facing and engineering documentation
scripts/         Local development automation scripts
database/        Reserved database assets
plc-service/     Reserved PLC service workspace
worker/          Reserved background worker workspace
```

## Local Development

### Prerequisites

- .NET SDK 10
- Node.js and npm
- Windows PowerShell

### Backend

Preferred startup from repo root:

```powershell
.\scripts\dev.ps1
```

This starts backend and frontend in separate terminals and runs terminal-based
health/API verification checks.

Manual backend startup:

```powershell
Set-Location backend
dotnet run --launch-profile http
```

Default backend URL: `http://localhost:5265`

If the default instance is blocked by an existing process, Windows service, or
single-instance SQLite lock, start an isolated backend for Siemens testing:

```powershell
.\scripts\dev.ps1 -Isolated
```

Manual isolated backend startup:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5266'
$env:ConnectionStrings__PlantMonitor='Data Source=C:\Users\Gabe\Desktop\PlantMonitor\backend\plantmonitor.dev.db'
dotnet run --no-launch-profile --project C:\Users\Gabe\Desktop\PlantMonitor\backend\backend.csproj
```

This isolated mode uses:

- port `5266` instead of `5265`
- a separate local SQLite file `backend/plantmonitor.dev.db`
- Development environment settings so localhost requests work normally

### Frontend

```powershell
Set-Location frontend
npm install
npm run dev
```

Default frontend URL: `http://127.0.0.1:5173`

In local development, Vite proxies `/api/*` and `/hubs/*` to `http://127.0.0.1:5265` by default.
To override this backend target, set `VITE_DEV_BACKEND_ORIGIN` before `npm run dev`.

Example for the isolated Siemens backend on `5266`:

```powershell
Set-Location frontend
$env:VITE_DEV_BACKEND_ORIGIN='http://127.0.0.1:5266'
npm run dev
```

### Helper script

```powershell
.\scripts\dev.ps1
```

## Hosted LAN Deployment

Use the backend-hosted SPA plus Windows service path for same-wired-network
access from multiple PCs.

Publish the hosted release:

```powershell
.\scripts\publish-host-release.ps1
```

Optional pre-install smoke test (development-only test credentials):

```powershell
.\scripts\test-host-release.ps1
```

Note: this smoke test runs the published release in `Development` environment
and uses `test` / `test` credentials.

Install the LAN Windows service:

```powershell
.\scripts\install-lan-service.ps1 -JwtSigningKey '<strong-32+-char-key>'
```

Validate the installed service:

```powershell
.\scripts\test-lan-service.ps1
```

Validate service plus credential login/session checks:

```powershell
.\scripts\test-lan-service.ps1 -Username operator -Password test
```

This is the canonical path for multiple simultaneous Status Board displays and
manager/reporting access from other computers on the same wired network.

## Default Development Accounts

- Admin: `test` / `test`
- Operator: `operator` / `test`
- Viewer: `viewer` / `test`

## Main Runtime APIs

- `POST /api/auth/login`
- `GET /api/dashboard`
- `GET /api/lines/{lineId}/details`
- `GET /api/production-runs`
- `GET /api/reports/events`

## Notes

- Disconnected PLC lines are reported as `Offline`.
- Adding new EF entities requires a migration before app startup will succeed.
- The runtime service is the source of truth for dashboard status, control mode, and report events.
- UI theme defaults to light mode unless the user has already saved a different preference in browser storage.

## Quality Checks

### Frontend unit tests

```powershell
Set-Location frontend
npm run test:run
```

### Frontend lint

```powershell
Set-Location frontend
npm run lint
```

### Frontend production build

```powershell
Set-Location frontend
npm run build
```

### End-to-end smoke tests

```powershell
Set-Location frontend
npm run test:e2e
```

The Playwright smoke suite validates:

- admin login
- invalid login handling
- operator route restriction
- theme persistence
- dashboard rendering after login

## Known Development Notes

- If `dotnet run` fails on port `5265`, another backend process is usually already listening there.
- If `dotnet run` fails with a PlantMonitor single-instance or SQLite lock error, use the isolated `5266` plus `plantmonitor.dev.db` flow above.
- If local backend builds fail with `MSB3027` or `MSB3021`, a running backend process may still have the output assembly locked.
- The frontend now expires stale sessions when protected APIs return `401`, so an outdated browser tab may redirect back to login after refresh.
- `docker-compose.yml` currently exists but is empty.

## Documentation

Additional project docs live under `docs/`:

- `docs/Api.md`
- `docs/Architecture.md`
- `docs/Database.md`
- `docs/Deployment.md`
- `docs/QA-Matrix.md`
- `docs/Release.md`
