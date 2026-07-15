# PlantMonitor Developer Log

## Project Intent
PlantMonitor is being productized as a vertical wire-manufacturing monitoring platform that runs on an on-prem server computer and ingests PLC tag data over Ethernet from a plant network.

Primary operating model:
- Server-hosted backend API + SignalR service inside the plant LAN
- Browser-based frontend for operations, supervisors, and admins
- PLC telemetry ingestion through adapter services (mock now, real adapters next)

## Current Status
- Branch: tagsort
- Stage: Runtime integration, reporting, and maintainability cleanup
- Focus: Live PLC-driven dashboard behavior, reporting history, offline handling, and codebase documentation

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
Last updated: 2026-07-15
