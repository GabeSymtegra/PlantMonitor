# PlantMonitor Deployment Runbook

## 1. Scope

This runbook covers deployment and operations for backend and frontend in the current mock PLC phase.

Included:

- Local developer startup.
- Private server deployment topology.
- Environment configuration.
- Health checks and operational validation.

Not included:

- Real PLC driver process deployment (future phase).
- External cloud hosting profiles.

## 2. Environment Profiles

### 2.1 Local Development

Purpose:

- Fast frontend and backend iteration using mock telemetry.

Services:

- frontend (Vite dev server)
- backend (ASP.NET API + SignalR)
- sqlite (local file, EF Core migrations on startup)

### 2.2 Private Production Server

Purpose:

- Internal plant network access for browser clients and backend data ingestion.

Services:

- frontend static build served by backend static files + SPA fallback
- backend API process
- sqlite database file in ProgramData

Network assumptions:

- Server is inside private plant network.
- Backend can reach configured line IP addresses.
- Public internet exposure is disabled unless explicitly required.

## 3. Required Configuration

### 3.1 Backend Settings

Required environment variables:

- ASPNETCORE_ENVIRONMENT
- ConnectionStrings__PlantMonitor
- Jwt__Issuer
- Jwt__Audience
- Jwt__SigningKey
- App__CorsOrigins

Required first production startup variable:

- Auth__BootstrapAdminPassword

Recommended values by environment:

- Local: ASPNETCORE_ENVIRONMENT=Development
- Production: configure Jwt and Auth bootstrap secret, and set explicit App__CorsOrigins entries.

### 3.2 Frontend Settings

Required environment variables:

- VITE_API_BASE_URL
- VITE_SIGNALR_HUB_URL

Local example:

- VITE_API_BASE_URL=http://localhost:5265/api
- VITE_SIGNALR_HUB_URL=http://localhost:5265/hubs/lines

### 3.3 Database Settings

Required:

- Writable local filesystem for ProgramData
- Backup location for sqlite file snapshots
- Service account permissions to read/write the database path

## 4. Local Startup Procedure

1. Ensure backend working directory and ProgramData paths are writable.
2. Set backend environment variables.
3. Run backend from backend directory.
4. Verify migrations apply on startup.
5. Set frontend environment variables.
6. Run frontend from frontend directory.
7. Open web app and verify dashboard loads line rows from backend.

Operational check after startup:

- Login endpoint responds.
- GET dashboard endpoint responds.
- SignalR connection opens and receives updates.

### 4.1 Clean Developer Startup Commands

Default backend:

```powershell
Set-Location backend
dotnet run --launch-profile http
```

Default frontend:

```powershell
Set-Location frontend
npm run dev
```

### 4.2 Isolated Siemens Test Startup

Use this when the default backend is already running, the Windows service is
active, or the shared SQLite database is locked.

Backend:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5266'
$env:ConnectionStrings__PlantMonitor='Data Source=C:\Users\Gabe\Desktop\PlantMonitor\backend\plantmonitor.dev.db'
dotnet run --no-launch-profile --project C:\Users\Gabe\Desktop\PlantMonitor\backend\backend.csproj
```

Frontend:

```powershell
Set-Location frontend
$env:VITE_DEV_BACKEND_ORIGIN='http://127.0.0.1:5266'
npm run dev
```

Why this mode exists:

- avoids port `5265` collisions
- avoids single-instance database lock conflicts
- keeps Siemens integration tests isolated from the installed/service-backed instance

## 5. Production Deployment Topology

Recommended topology:

- Reverse proxy terminates TLS.
- Reverse proxy routes:
	- /api and /hubs to backend service
	- / to backend-hosted frontend static content
- Backend runs on private subnet.

Security controls:

- Allowlist frontend origin in CORS.
- Rotate JWT signing key by policy.
- Restrict access to backend host and local sqlite storage path.

## 6. Build And Release Process

### 6.1 Backend Release

Release steps:

1. Build backend in release mode.
2. Run tests.
3. Start backend once and verify migration output.
4. Deploy backend binaries.
5. Restart backend service.
6. Validate health and logs.

### 6.2 Frontend Release

Release steps:

1. Install dependencies.
2. Build production assets.
3. Publish assets to backend wwwroot.
4. Purge cache if reverse proxy or CDN cache is enabled.
5. Validate dashboard page and SignalR connectivity.

## 7. Health And Observability

### 7.1 Health Endpoints

Required backend health endpoints:

- /health/live
- /health/ready

Readiness should validate:

- Database connectivity.
- SignalR subsystem registration.
- Runtime service initialization.

### 7.2 Logging Requirements

Minimum structured log events:

- auth.login.success
- auth.login.failure
- api.request.failed
- dashboard.snapshot.generated
- signalr.client.connected
- signalr.broadcast.sent
- telemetry.mock.tick

Each log entry should include traceId and timestamp.

### 7.3 Alerting Triggers

Initial alerts:

- Backend unavailable for more than 1 minute.
- Database connection failure.
- SignalR broadcast failures exceeding threshold.
- Dashboard data freshness older than configured threshold.

## 8. Operational Procedures

### 8.1 Rollback

Rollback strategy:

1. Deploy previous backend build.
2. Restore previous frontend assets.
3. If migration introduced incompatible change, run tested rollback migration script.
4. Validate endpoints and dashboard rendering.

### 8.2 Incident Triage Checklist

If dashboard is stale:

1. Verify backend process state.
2. Verify database connectivity.
3. Verify /api/dashboard response.
4. Verify SignalR connection and events.
5. Inspect telemetry producer logs.

If login fails:

1. Verify JWT config values.
2. Verify user and role records.
3. Inspect auth failure logs by traceId.

## 9. Environment Promotion Checklist

Before promoting to next environment:

1. Migrations applied successfully.
2. Seed data present where required.
3. Auth policies verified by role.
4. Dashboard and report APIs verified.
5. Realtime update flow verified with multiple clients.
6. Runbook steps validated by another team member.

## 10. Acceptance Criteria

1. New environment can be provisioned from this document without hidden steps.
2. Frontend and backend can run together with configured API and hub URLs.
3. Operational team can detect and triage stale dashboard conditions.
4. Deployment process includes repeatable rollback path.
5. Documentation reflects monitor-only behavior in current phase.

