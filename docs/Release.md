# PlantMonitor Release Process

## Scope
This document defines release gates for shipping PlantMonitor to a server-hosted, plant-network deployment where PLC tag data is ingested over Ethernet.

## Versioning
- Use semantic pre-release tags until GA.
- Current stream: 0.1.0-alpha
- Promotion flow:
  - alpha -> beta -> rc -> ga

## Release Branch Strategy
- main: stable release branch
- export (current): active integration branch
- release/*: staged hardening and final verification

## Required Gates Before Release
1. Backend build and tests pass.
2. Frontend build and tests pass.
3. E2E smoke suite passes.
4. Security advisories in release-critical dependencies are triaged and resolved or accepted with documented waiver.
5. Release checklist in docs/QA-Matrix.md marked complete.
6. Server deployment runbook steps validated on a staging-like host.

## CI Required Checks Mapping
- Workflow: .github/workflows/ci.yml
- Job: build-test
- Required command coverage:
  - dotnet build backend/backend.csproj --configuration Release --no-restore
  - dotnet test backend.Tests/backend.Tests.csproj --configuration Release --no-build
  - npm run test:run (frontend)
  - npm run build (frontend)
  - npm run test:e2e (frontend)

## Branch Protection Guidance
- Protect main with required status check: build-test.
- Require pull request before merge.
- Require branch to be up to date before merge.
- Disable force-push and branch deletion on protected branches.
- Include release/* protection once release branches are created.

## Artifact Expectations
- Backend release binaries (self-contained or framework-dependent, documented choice).
- Frontend production build assets.
- Environment variable template for production.
- DB migration scripts and rollback notes.

## Pre-Deploy Validation
1. Confirm database connectivity and migration status.
2. Confirm backend health endpoints respond.
3. Confirm frontend can authenticate and load dashboard.
4. Confirm SignalR connection and update flow.
5. Confirm role gates: Viewer/Operator/Admin behavior.
6. Confirm server can reach PLC network endpoints configured for lines.

## Deployment Sequence
1. Backup database.
2. Deploy backend service update.
3. Apply migrations.
4. Deploy frontend assets.
5. Restart/reload services.
6. Run smoke checks.

## Rollback
1. Stop updated services.
2. Restore previous backend binaries and frontend assets.
3. Apply rollback migration if needed.
4. Restart and re-run smoke checks.

## Operational Sign-Off
Release owner records:
- Release ID/version
- Build SHA
- Date/time
- Validation operator
- Outstanding risk notes
