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