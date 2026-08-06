# Scripts

This folder contains local development scripts.

## Current Scripts

- `dev.ps1`: start backend and frontend for local development
- `dev-stop.ps1`: stop local listeners and dev processes
- `cleanup-ports.ps1`: clear blocked development ports
- `pre-deploy-check.ps1`: run deployment readiness checks
- `db-backup.ps1`: create timestamped SQLite backups with retention cleanup
- `db-restore.ps1`: restore SQLite database from a selected or latest backup
- `build-installer.ps1`: build backend release and compile Windows installer using Inno Setup
- `package-release.ps1`: archive published release binaries into a timestamped zip package

Use these scripts instead of manual port cleanup when possible.

## Clean Local Startup

Normal backend plus frontend:

```powershell
.\scripts\dev.ps1
```

What this does:

- starts backend and frontend in separate PowerShell terminals
- aligns frontend proxy target to the same backend URL automatically
- runs startup verification checks for:
	- `GET /health/live`
	- `GET /health/ready`
	- `POST /api/auth/login`
	- `GET /api/lines`
	- `GET /api/dashboard`
	- frontend root URL

If default mode is blocked by a running service/process or a shared database lock,
run isolated mode instead:

```powershell
.\scripts\dev.ps1 -Isolated
```

Optional isolated database override:

```powershell
.\scripts\dev.ps1 -Isolated -IsolatedDatabasePath 'C:\path\to\custom\plantmonitor.dev.db'
```

If the default backend port or database is already in use, use this manual
isolated Siemens-testing flow:

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