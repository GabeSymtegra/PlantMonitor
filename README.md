# PlantMonitor

PlantMonitor is a manufacturing line monitoring application with a React frontend and an ASP.NET Core backend. It supports authenticated administration, live PLC connectivity, line runtime tracking, status dashboards, and reporting for completed runs plus mode and status transitions.

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

```powershell
Set-Location backend
dotnet run --launch-profile http
```

Default backend URL: `http://localhost:5265`

If the default instance is blocked by an existing process, Windows service, or
single-instance SQLite lock, start an isolated backend for Siemens testing:

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

## Near-Term Gaps

- Replace frontend mock dashboard data with backend-driven snapshots.
- Replace filler PLC tag browsing with the real tag database.
- Add logical tag mapping UI and persistence beyond the current filler/browser contract.
- Add runtime polling and logical machine snapshot aggregation.
- Finalize production deployment and container orchestration assets.

- /
- /lines
- /lines/:id
- /products
- /reports
- /status-board
- /forbidden

### Admin Only

- /administration
- /settings

## API Summary (Current)

- POST /api/auth/login
- GET /api/status (authorized)
- GET /api/configuration/access-check (admin policy)
- SignalR hub: /hubs/lines (authorized)

## Local Development

### Prerequisites

- .NET SDK 10
- Node.js 20+
- npm

### Run Frontend

1. Open terminal in frontend
2. Run npm install
3. Run npm run dev

### Run Backend

1. Open terminal in backend
2. Run dotnet restore
3. Run dotnet run

If build/run fails due file locks on backend.exe or backend.dll, stop the running backend process and retry.

## Build Commands

- Frontend: npm run build
- Backend: dotnet build

## Environment Configuration

### Frontend

- VITE_API_BASE_URL controls API base URL for frontend requests.
- VITE_DEV_BACKEND_ORIGIN controls Vite dev proxy target for `/api` and `/hubs`.

### Backend

- JWT settings are read from appsettings via Jwt configuration section.

## Security Notes

- Current credentials are development-only and should be replaced before production.
- CORS is currently configured permissively for development.
- A dependency warning may appear for Microsoft.OpenApi package vulnerability advisory; track and update package versions as part of hardening.

## Documentation

Additional docs are available in docs:

- docs/Api.md
- docs/Architecture.md
- docs/Database.md
- docs/Deployment.md

## Author

Gabriel Carswell
