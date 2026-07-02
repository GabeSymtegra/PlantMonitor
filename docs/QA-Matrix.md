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
- Dark mode auto-applies Spec Ops preset.
- Spec Ops surfaces keep readable text contrast.
- Theme persists across reload.

### Server and Network Readiness
- Backend service starts on target server host.
- Frontend can reach backend API and hub.
- Server can reach configured PLC IP addresses over Ethernet.
- PLC tag ingest pipeline reports valid freshness.
- Stale-data detection and logs are visible.

## Release Checklist
- [ ] Backend build passes.
- [ ] Frontend build passes.
- [ ] Backend automated tests pass.
- [ ] Frontend automated tests pass.
- [ ] E2E smoke suite passes.
- [ ] Security advisory review complete.
- [ ] Deployment runbook verified.
- [ ] Rollback procedure validated.

## Evidence Capture
For each release, capture:
- Build logs
- Test reports
- Smoke test screenshots/logs
- Server commissioning notes
- PLC connectivity validation output

## Sign-Off
- QA Owner:
- Release Owner:
- Date:
- Version:
- Notes:
