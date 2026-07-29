# PlantMonitor Developer Log

## Project Intent
PlantMonitor is being productized as a vertical wire-manufacturing monitoring platform that runs on an on-prem server computer and ingests PLC tag data over Ethernet from a plant network.

Primary operating model:
- Server-hosted backend API + SignalR service inside the plant LAN
- Browser-based frontend for operations, supervisors, and admins
- PLC telemetry ingestion through adapter services (mock now, real adapters next)

## Current Status
- Branch: export
- Stage: Runtime integration, local launch reliability, and maintainability cleanup
- Focus: Live PLC-driven dashboard behavior, reporting history, offline handling, local dev startup, and codebase documentation

## Production Readiness Pass (2026-07-29)
- Verified the frontend publish/runtime path by serving `frontend/dist` through backend `wwwroot` and checking referenced JS/CSS assets by real HTTP request.
- Verified production-auth behavior in live browser flows, including login through the frontend origin and authenticated dashboard access after cookie-based sign-in.
- Confirmed the current local production bootstrap path still requires the development accounts for local testing, while the production-only bootstrap guard remains a deployment concern.
- Fixed the frontend dev proxy so `/api` and `/hubs` requests route to the backend during local development.
- Fixed the PowerShell port cleanup helper used before local launches.
- Deferred installer work until application correctness and publish/runtime validation are complete.
- Verified backend test coverage for the fixed stop-ship slice, including mode-duration attribution, PLC preset cleanup, and PLC connection/config validation.
- Extracted completed-run and runtime-event reporting queries into a dedicated reports service so ProductionRuntimeService now stays focused on live polling and snapshots.
- Verified the backend test suite still passes after the reports-service split.
- Standardized ProblemDetails responses for production/report query validation and not-found paths (`/api/lines/{id}/details`, `/api/production-runs`, `/api/production-runs/{id}`, `/api/reports/events`) with consistent status/title/detail/type and machine-readable `code` extension.
- Added backend API test assertions for the new ProblemDetails payload contract and re-ran the full backend test suite.
- Expanded ProblemDetails standardization across auth, lines, and admin PLC endpoints, replacing remaining ad-hoc `{ message }` payloads with consistent RFC7807 responses and stable error `code` extensions.
- Implemented SignalR meaningful-change suppression so hub broadcast notifications fire on state-signature changes (with heartbeat fallback) instead of unconditional timer-only fan-out.
- Hardened test reliability for auth/login by adding a test-only configuration switch that disables login rate-limiting in integration tests while preserving production behavior.
- Improved readiness failure semantics by returning ProblemDetails for `/health/ready` database-unavailable responses.
- Refreshed API and release docs to align with current cookie-auth and ProblemDetails behavior, including current integration-branch naming.
- Validated backup and restore scripts end-to-end using test database artifacts (`db-backup.ps1`, `db-restore.ps1`) with successful backup creation and restore copy execution.
- Expanded reports UX with preset date ranges and aggregate summary views for completed runs and runtime events.
- Added backend API coverage for admin auto-map endpoint behavior and validated it in the backend integration suite.
- Added Windows installer build automation (`scripts/build-installer.ps1`) plus Inno Setup definition (`installer/PlantMonitor.iss`) and successfully produced `artifacts/installer/PlantMonitor-Setup.exe`.
- Added release packaging automation (`scripts/package-release.ps1`) and produced a versioned archive at `artifacts/packages/PlantMonitor-0.1.0-alpha-20260729-141034.zip`.
- Added commissioning hardening endpoint `GET /api/admin/lines/{lineId}/commissioning-check` to validate required tag-slot readiness per line and surface missing logical keys/issue details.
- Added backend integration coverage for commissioning readiness and validated end-to-end via admin assignment + commissioning-check flow.
- Added runtime API correctness coverage for dashboard/detail contracts (`backend.Tests/RuntimeApiTests.cs`) and validated runtime endpoint ProblemDetails/shape behavior.
- Performed live system verification with running frontend (`http://127.0.0.1:5173`) and backend (`http://127.0.0.1:5265`): root, health, login, dashboard, admin presets, production-runs, backend-served SPA assets, and SignalR `/hubs/lines/negotiate` all returned `200`.
- Fixed frontend API success handling for empty responses (`204`, empty body, and `Content-Length: 0`) so successful no-content auth flows no longer throw JSON parse errors; updated change-password to `apiPost<void>()` and validated redirect behavior with new frontend tests.
- Fixed admin auto-map first-attempt behavior by passing the fresh successful PLC connection result directly into auto-map/discovery flow instead of relying on same-tick React state, and added frontend test coverage for connection -> discovery -> auto-map orchestration.

## Production Readiness Checklist Snapshot (2026-07-29)
- Done: frontend publishing now builds and copies `frontend/dist` into backend publish output.
- Done: published release assets were verified over HTTP, including `/`, `/health/live`, `/health/ready`, and referenced JS/CSS files.
- Done: auth is cookie-only and login/session responses no longer return JWTs in JSON.
- Done: forced password-change gating is enforced on protected API routes and returns ProblemDetails.
- Done: backend auth and API tests were converted to cookie-based session requests and pass.
- Done: frontend auth and route tests pass after the auth contract change.
- Done: frontend login, change-password, and protected-route flows were updated to match the new session model.
- Done: bootstrap admin seeding no longer blocks production restarts once an admin already exists.
- Done: allowed hosts were tightened away from wildcard defaults.
- Done: frontend dev proxy for `/api` and `/hubs` was fixed.
- Done: PowerShell launch and cleanup helpers were repaired.
- Done: runtime mode-duration attribution now credits elapsed time to the previously active mode.
- Done: bundled Siemens PLC presets were removed from the seed and fallback config paths.
- Done: PLC connection and tag-validation messaging now reflects Allen-Bradley-only production support.
- Done: backend integration tests now use isolated test databases to avoid stale lock/schema collisions.
- Done: completed-run and runtime-event queries were moved into a dedicated reports service.
- Done: production/report validation and not-found responses now use standardized ProblemDetails payloads with stable error codes.
- Done: remaining auth/admin/line validation failures now emit standardized ProblemDetails payloads with stable error codes.
- Done: SignalR refresh notifications now suppress non-meaningful state churn via snapshot-signature change detection.
- Done: health/readiness failure path now returns structured ProblemDetails (`503`) instead of an empty status body.
- Done: documentation refresh for API auth/error contract and release branch strategy completed to match current implementation.
- Done: backup/restore replacement is implemented and validated via `scripts/db-backup.ps1` and `scripts/db-restore.ps1` execution.
- Done: admin auto-map workflow is covered by backend integration tests and validated through the admin PLC API test suite.
- Done: reports expansion and cleanup implemented with date-range presets and aggregate run/event summaries.
- Done: Windows installer build is automated and validated with generated installer artifact.
- Done: repository cleanup and release packaging now has executable automation and validated packaged output.
- Done: PLC configuration and commissioning hardening includes explicit per-line commissioning readiness validation and test coverage.
- Done: runtime correctness beyond the auth gate now has explicit dashboard/detail API correctness tests and passing suite validation.

## Finalized Changes (Completed)

### Security and Access Control
- Implemented role-aware frontend route protection with admin-only gating for configuration pages.
- Added explicit access-denied flow with forbidden page UX for unauthorized roles.
- Restricted settings and administration visibility in navigation for non-admin users.
- Enforced backend admin policy checks for configuration access endpoint.

### Authentication and Realtime
- Hardened JWT behavior for SignalR by reading access_token query values on hub connections.
- Required authorization on the line hub endpoint.
- Improved frontend auth flow and state handling so login failure scenarios are clearer.

### CORS and Local Reliability
- Updated backend CORS policy to support localhost and 127.0.0.1 origins with credentials for local development, including dynamic Vite port fallback.
- Added PowerShell reliability scripts for deterministic local operations:
  - scripts/cleanup-ports.ps1
  - scripts/dev.ps1
  - scripts/dev-stop.ps1
  - scripts/pre-deploy-check.ps1

### Theme and Usability
- Added appearance controls and preset-based theme management.
- Implemented dark mode auto-switch to Spec Ops preset.
- Fixed low-contrast theme issues by applying computed readable foreground colors for:
  - App header text
  - Table header text
  - Preset chips and active preset button content
- Verified contrast behavior in live browser checks for Spec Ops surfaces.

### Dashboard and UI
- Added and refined status board view for floor-display usage.
- Iteratively tuned board/table readability and alignment behavior.

### Automated Test Baseline (Release Readiness)
- Added backend integration test project at backend.Tests with auth and authorization coverage:
  - Valid and invalid login behavior
  - Admin-only configuration endpoint behavior (401/403/200)
- Added frontend Vitest + Testing Library setup with initial smoke tests:
  - ProtectedRoute role-gate behavior
  - Dark mode forcing Spec Ops palette behavior

### Frontend Performance Baseline Kickoff
- Introduced route-level lazy loading in AppRoutes to begin chunk separation.
- Build now emits split route chunks for multiple pages, reducing single-bundle concentration and starting item 5 performance work.
- Added explicit Rollup manual chunk groups in Vite config for React, MUI, DataGrid, and SignalR dependencies.

### E2E Smoke and CI Gate
- Added Playwright smoke suite at frontend/e2e/smoke.spec.ts covering:
  - Valid admin login and dashboard load
  - Invalid login error behavior
  - Operator block on admin-only settings route
  - Dark mode persistence and enforced Spec Ops colors
  - Dashboard table header visibility
- Added Playwright configuration at frontend/playwright.config.ts with managed backend/frontend web servers for local and CI runs.
- Added GitHub Actions pipeline at .github/workflows/ci.yml to run backend build/tests, frontend unit tests/build, and Playwright smoke tests.
- Stabilized test tooling boundaries so Vitest only discovers src test files and does not execute e2e Playwright specs.
- Stabilized local Playwright smoke execution with single-worker mode and deterministic login assertions.
- Verified backend package graph no longer contains Microsoft.OpenApi; NU1903 warning path no longer appears in direct/transitive package listing.

### Runtime and Dashboard Reliability
- Stabilized ProductionRuntimeService so dashboard and line detail endpoints can continue serving snapshots even when the runtime engine is inactive or PLC reads fail.
- Fixed save-to-dashboard behavior so configured line/tag state can surface without requiring a separate test-connection flow.
- Added product ID normalization so unreadable PLC metadata payloads are shown as `N/A` instead of raw diagnostic JSON.
- Corrected control mode persistence so manual and auto state remain visible while lines are stopped.
- Fixed repeated stop-state persistence loops that were inserting duplicate completed-run records.
- Changed disconnected PLC behavior so failed PLC polling marks the line `Offline` instead of generating simulated live statuses.
- Updated the PLC failure path to emit runtime status transitions when moving into `Offline`.
- Added persisted active runtime checkpoints so line state restores after restart instead of remaining memory-only.
- Replaced synthetic runtime metadata with explicit line-configured `RecipeId`, `MachineId`, and `OperatorName` fields.
- Fixed mode attribution in statistics so auto/manual intervals are credited from the sample mode instead of the previous cached mode.
- Tightened lifecycle locking and checkpoint persistence in the runtime loop to reduce state races.

### Local Launch and Live Validation
- Fixed the frontend dev API path so `/api` and `/hubs` requests proxy to the backend during local development.
- Fixed the PowerShell port cleanup helper so it can safely clear stale listeners before launching the stack.
- Verified live browser login through the frontend origin using the built-in dev accounts.
- Verified authenticated dashboard access through the frontend proxy after login.
- Confirmed direct backend auth, frontend-proxied auth, and authenticated dashboard API access all return `200` in live checks.
- Confirmed the dashboard route loads after sign-in and renders live line data in the browser.

### PLC State Mapping and Runtime Semantics
- Implemented explicit machine-state decoding for live PLC values:
  - `0 => Stopped`
  - `1 => Running`
  - `2 => Bleedout`
  - `3 => Startup`
  - `4 => Faulted`
  - `5 => Maintenance`
- Extended frontend status handling so `Bleedout` and `Startup` render correctly across navbar summaries, dashboard tables, line lists, status chips, and the status board.
- Changed time-in-status behavior to reset when the line status changes, so runtime reflects duration in the current state rather than total run age.

### Reporting and Historical Data
- Added completed production run persistence for historical reporting.
- Added runtime event persistence for status switches and mode switches across all lines.
- Added backend reporting API coverage for:
  - completed runs
  - runtime switch events
- Added the frontend Reports page with:
  - completed run history
  - mode and status switch history
  - line filtering
  - date range filtering
  - CSV export for both tables
- Added EF migration support for the new runtime event persistence model.

### Codebase Cleanup and Documentation
- Performed a low-risk cleanup pass on PLC tag address validation to reduce duplication and make manufacturer-specific rules easier to read.
- Replaced the placeholder frontend README with project-specific guidance.
- Added folder-level READMEs to the main backend, frontend, docs, and scripts directories so the code layout is easier to understand.
- Added a user guide in docs/User-Guide.md and linked it from the root README.
- Updated the root README to reflect the current architecture, runtime behavior, reports support, and documentation map.
- Updated the developer log with the latest runtime persistence, frontend proxy, and live-verification work.

## Release Readiness Roadmap (Items 1-6)

## 1) Automated Auth and Role Tests
Objective:
- Prevent regressions in role policy and authentication behavior.

Planned deliverables:
- Backend test project for JWT, role policy, and protected endpoint behavior.
- Frontend unit tests for login/session and protected route behavior.

Exit criteria:
- Viewer and Operator blocked from admin-only routes and APIs.
- Admin access validated across frontend and backend.

## 2) Dev and Runtime Reliability Scripts
Objective:
- Make local startup and shutdown deterministic.

Planned deliverables:
- Scripts to start frontend/backend with health checks.
- Scripts to stop services and clean stale processes/ports.

Exit criteria:
- One command boots local stack.
- One command shuts down cleanly without lock artifacts.

Status update:
- `scripts/dev.ps1` now starts the frontend and backend for local development.
- `scripts/cleanup-ports.ps1` was fixed to clear stale listeners correctly.
- The browser-login flow was verified end-to-end after the proxy fix.

## 3) End-to-End UI Smoke Tests
Objective:
- Automate critical behavior checks across real browser flows.

Planned smoke suite:
- Login success and failure behavior
- Dark mode -> Spec Ops enforcement and persistence
- Admin route denial for non-admin users
- Dashboard table rendering and basic interaction

Exit criteria:
- Smoke suite passes in CI and locally before release.

## 4) Security Warning Remediation
Objective:
- Remove NU1903 advisory path from backend dependency graph.

Planned deliverables:
- Remove or upgrade vulnerable package chain.
- Rebuild validation with advisory no longer reported.

Exit criteria:
- dotnet restore/build no longer reports NU1903.

## 5) Frontend Performance Baseline and Chunking
Objective:
- Reduce initial bundle cost and establish measurable targets.

Planned deliverables:
- Baseline bundle report.
- Route-level lazy loading and chunk strategy.
- Reduced duplicate refresh behavior where applicable.

Exit criteria:
- Documented before/after bundle metrics.
- Improved startup payload profile.

## 6) Release Checklist and QA Matrix
Objective:
- Define a repeatable gate for deployment readiness.

Planned deliverables:
- Release checklist document
- QA matrix covering auth, dashboard, realtime, themes, and role boundaries
- Server deployment gate for plant-network operation

Exit criteria:
- Pre-release checklist completed and signed off.

Status update:
- The remediation checklist was updated with the latest fixed runtime, frontend, and documentation items.
- Live validation evidence was added through direct backend, frontend-proxy, and browser checks.

## Deployment Readiness for Server + PLC Ethernet

### Target Installation Model
- Backend installed as a service on a dedicated plant server computer.
- Frontend served from same server (or trusted internal host) and configured to call backend over LAN.
- PLC network reachable from server NIC/VLAN and firewall-approved routes.

### PLC Tag Ingestion Requirements
- Adapter contract normalizes PLC tag inputs into canonical telemetry fields.
- Connection metadata tracked per line (IP, PLC family, package/driver version, poll interval).
- Data quality checks applied before broadcasting updates to clients.

### Network and Operations Controls
- CORS restricted to approved internal origins in production.
- Firewall rules explicit for backend/API/hub access and PLC communication as required by PLC family.
- Service startup dependencies verified (database, network path to PLCs, config availability).
- Logging includes connectivity, ingest errors, stale data age, and broadcast health.

### Production Validation Gates
- Server can reach configured PLC IPs over Ethernet.
- Tag data enters backend and appears in dashboard with expected freshness.
- Role policy enforcement holds for all UI and API configuration paths.
- Realtime updates resilient under reconnect scenarios.

## Known Risks and Active Mitigations
- Risk: Process lock conflicts during backend rebuild/run cycles.
  - Mitigation: Add scripted stop/cleanup flow and pre-run checks.
- Risk: Missing test infrastructure for auth and role regressions.
  - Mitigation: Establish backend + frontend test projects first in implementation sequence.
- Risk: Performance drift as features expand.
  - Mitigation: Baseline bundles and enforce chunking strategy.

## Next Implementation Slice
Immediate next coding slice:
1. Break up ProductionRuntimeService into smaller units by responsibility to reduce maintenance cost.
2. Add focused tests around offline transitions, machine-state decoding, and report event persistence.
3. Expand reports with preset date ranges and optional aggregation views.
4. Continue replacing remaining outdated assumptions in docs and log files with current runtime behavior.

---
Last updated: 2026-07-29
