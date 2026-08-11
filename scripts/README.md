# Scripts

This folder contains local development scripts.

## Current Scripts

- `dev.ps1`: start backend and frontend for local development
- `dev_private.ps1`: start backend and frontend for private-network/LAN testing
- `dev-stop.ps1`: stop local listeners and dev processes
- `cleanup-ports.ps1`: clear blocked development ports
- `pre-deploy-check.ps1`: run deployment readiness checks
- `db-backup.ps1`: create timestamped SQLite backups with retention cleanup
- `db-restore.ps1`: restore SQLite database from a selected or latest backup
- `build-installer.ps1`: build backend release and compile Windows installer using Inno Setup
- `package-release.ps1`: archive published release binaries into a timestamped zip package
- `publish-host-release.ps1`: publish the backend-hosted frontend release without building the installer
- `test-host-release.ps1`: run smoke verification against the published backend-hosted release
- `install-lan-service.ps1`: install the published backend-hosted app as a Windows service for same-network access
- `test-lan-service.ps1`: validate the installed LAN Windows service and hosted SPA/API endpoints
- `uninstall-lan-service.ps1`: remove the LAN Windows service and optionally its data directory

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

## Private Host Startup

Use this to make dev servers reachable from other devices on your private
network while avoiding shared local database locks.

```powershell
.\scripts\dev_private.ps1
```

Defaults:

- backend bind: `http://0.0.0.0:5267`
- frontend bind: `http://0.0.0.0:5174`
- isolated database: `backend/plantmonitor.private.dev.db`

The script prints LAN-access URLs and runs the same startup verification checks
as `dev.ps1`.

## LAN Hosted Service

Use this for the first production-like same-network rollout on one host machine.
Publish the backend-hosted release first, then install it as a Windows service.

Example:

```powershell
dotnet publish backend/backend.csproj -c Release -o artifacts/release/backend
.\scripts\install-lan-service.ps1 -JwtSigningKey '<strong-32+-char-key>' -BootstrapAdminPassword '<initial-admin-password>' -BootstrapViewerPassword '<viewer-password>'
```

Canonical script-based flow:

```powershell
.\scripts\publish-host-release.ps1
.\scripts\install-lan-service.ps1 -JwtSigningKey '<strong-32+-char-key>' -BootstrapAdminPassword '<initial-admin-password>' -BootstrapViewerPassword '<viewer-password>'
.\scripts\test-lan-service.ps1
```

Optional convenience account for remote operator testing (username `test`, password defaults to `test`):

```powershell
.\scripts\install-lan-service.ps1 -JwtSigningKey '<strong-32+-char-key>' -BootstrapAdminPassword '<initial-admin-password>' -BootstrapViewerPassword '<viewer-password>' -EnableTestAccount
```

Defaults:

- service name: `PlantMonitor-LAN`
- bind URL: `http://0.0.0.0:5050`
- install directory: `C:\Program Files\PlantMonitor`
- data directory: `C:\ProgramData\PlantMonitor`
- LAN deployment mode enabled via `App__IsLanDeployment=true`

For first production-style LAN installs, provide both:

- `BootstrapAdminPassword` for the initial administrator account
- `BootstrapViewerPassword` for the shared Viewer account used by status-board displays

The script verifies `/health/live` and `/health/ready`, opens the firewall on the chosen port for private profiles, and prints example LAN URLs for other PCs on the same wired network.

Validate the installed LAN service:

```powershell
.\scripts\test-lan-service.ps1
```

Validate LAN service plus credential login/session behavior:

```powershell
.\scripts\test-lan-service.ps1 -Username test -Password test
```

Use credential checks when you need to verify remote sign-in behavior end-to-end.

Remove the LAN service but preserve data by default:

```powershell
.\scripts\uninstall-lan-service.ps1
```

Remove the LAN service and all stored data:

```powershell
.\scripts\uninstall-lan-service.ps1 -RemoveData -ConfirmRemoveData REMOVE
```

## Hosted Release Publish And Smoke Test

Use these when iterating on the backend-hosted LAN path without rebuilding the installer each time.

Publish the release:

```powershell
.\scripts\publish-host-release.ps1
```

Defaults:

- runtime identifier: `win-x64`
- self-contained: `true`

Smoke-test the published release locally in LAN mode:

```powershell
.\scripts\test-host-release.ps1
```

Note: `test-host-release.ps1` runs a temporary local instance of the published
release in `Development` environment. It is intended for pre-install binary
verification and uses development test credentials, which differ from LAN
bootstrap accounts configured by `install-lan-service.ps1`.

This verifies:

- `GET /health/live`
- `GET /health/ready`
- `POST /api/auth/login`
- `GET /api/lines`
- `GET /api/dashboard`
- frontend root `/`