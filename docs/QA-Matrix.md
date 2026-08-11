# PlantMonitor QA Matrix

## Goal
Provide a repeatable pass/fail matrix for release readiness of PlantMonitor in a server-hosted PLC Ethernet environment.

## Test Areas

### Authentication and Authorization
- Login success with valid credentials.
- Login failure with invalid credentials.
- Viewer blocked from admin configuration routes.
- Operator blocked from admin configuration routes.
- Admin can access configuration routes.
- API admin-only endpoint returns forbidden for non-admin.

### Dashboard and Realtime
- Dashboard initial load returns line data.
- Table and card views both render expected content.
- SignalR connection establishes after auth.
- Realtime refresh events update dashboard.
- Reconnect behavior recovers after temporary disconnect.

### Theme and Accessibility Behavior
- Default theme is light mode unless the user has already saved a different preference in browser storage.
- Dark mode applies the Spec Ops preset when selected.
- Spec Ops surfaces keep readable text contrast.
- Theme persists across reload.

### Server and Network Readiness
- Backend service starts on target server host.
- Frontend can reach backend API and hub.
- LAN service credential validation passes using seeded credentials (`admin`/`test`, `operator`/`test`, `viewer`/`test`).
- Server can reach configured PLC IP addresses over Ethernet.
- PLC tag ingest pipeline reports valid freshness.
- Stale-data detection and logs are visible.

## Release Checklist
- [x] Backend build passes.
- [x] Frontend build passes.
- [x] Backend automated tests pass.
- [x] Frontend automated tests pass.
- [x] E2E smoke suite passes.
- [x] Security advisory review complete.
- [x] Deployment runbook verified.
- [x] Rollback procedure validated.

## Implementation Slice Checklist
- [x] Slice 1: Factory Wi-Fi connectivity diagnostics API + dashboard panel.
- [x] Slice 2: Read-only OTA release check API + dashboard panel.
- [x] Slice 3: Admin re-auth token flow + server-enforced OTA prepare-apply gate.
- [x] Slice 4: OTA package staging/download verification workflow.
- [x] Slice 5: OTA apply execution, post-apply health check, and automatic rollback.
- [x] Slice 6: Privileged host agent scaffold + Wi-Fi status/scan/connect/disconnect admin proxy and dashboard controls.
- [x] Slice 7: Dual-service packaging/install flow (backend + privileged agent), startup dependency wiring, and LAN verification script updates.
- [x] Slice 8: Integrated commissioning evidence pass (role gate check, Wi-Fi admin read checks, OTA forced-failure rollback, and post-apply continuity validation).
- [x] Slice 9: Move all network diagnostics and Wi-Fi management controls from Administration to Settings.
- [x] Slice 10: Remove bootstrap password variability and enforce fixed LAN seeded credentials (`admin`/`test`, `operator`/`test`, `viewer`/`test`).

## Evidence Capture
For each release, capture:
- Build logs
- Test reports
- Smoke test screenshots/logs
- Server commissioning notes
- PLC connectivity validation output

Latest validation evidence (2026-08-11):
- `scripts/publish-host-release.ps1` succeeded for backend + privileged agent payloads.
- `scripts/install-lan-service.ps1` succeeded with both services running (`PlantMonitor-LAN`, `PlantMonitor-PrivilegedAgent`).
- `scripts/test-lan-service.ps1 -Username operator -Password test` passed credential validation checks.
- Frontend unit/integration tests: `npm run test:run` -> 19/19 passed.
- Frontend E2E smoke: `npm run test:e2e` -> 6/6 passed (includes Settings network-controls visibility check).
- Security advisory checks: `dotnet list backend/backend.csproj package --vulnerable` -> no vulnerable packages; `npm audit --omit=dev` -> 0 vulnerabilities.
- Rollback validation: live staged OTA apply with `forceHealthFailure=true` returned `status=rolled_back` and `rolledBack=true`.
- Integrated commissioning scenario: admin login `200`, operator login `200`, operator blocked from admin endpoint (`403`), Wi-Fi status/scan admin checks `200/200` (`scanCount=2`), OTA stage `staged`, OTA apply `rolled_back=true`, and continuity checks remained healthy (`/health/live=200`, `/health/ready=200`, `/api/dashboard=200`).
- Post-change LAN install validation: `install-lan-service.ps1` runs with no bootstrap password parameters; `test-lan-service.ps1 -Username admin -Password test` passed required credential and Wi-Fi endpoint checks.

## Sign-Off
- QA Owner:
- Release Owner:
- Date:
- Version:
- Notes:
