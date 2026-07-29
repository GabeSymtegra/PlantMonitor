# PlantMonitor Remediation Checklist

Status values:
- Pending
- Fixed
- Deferred (owner-approved)
- Blocked (with evidence)

## 1. Critical authorization and authentication
- 1.1 Completed run deletion authorization: Fixed
- 1.2 Hardcoded accounts and plaintext passwords: Fixed
- 1.3 Placeholder JWT signing key: Fixed
- 1.4 Login abuse protections: Fixed
- 1.5 Token storage and SignalR token exposure: Fixed
- 1.6 HTTPS production configuration: Fixed
- 1.7 AllowedHosts and security headers: Fixed

## 2. PLC correctness and safety
- 2.1 AB repeated discovery per tag: Fixed
- 2.2 Sequential polling stalls all lines: Fixed
- 2.3 AB path/hardware assumptions fixed: Blocked (with evidence)
- 2.4 Structured tags treated as scalar: Blocked (with evidence)
- 2.5 Siemens placeholder read/browse mismatch: Fixed
- 2.6 Fake PLC write success and write endpoint: Fixed
- 2.7 Connection test uses HTTP 200 for failure outcome: Fixed
- 2.8 IP validation/message mismatch: Fixed

## 3. Runtime-state and configuration
- 3.1 Line config only loaded at startup: Fixed
- 3.2 Polling configuration ignored: Fixed
- 3.3 Active production state memory-only: Fixed
- 3.4 Product and recipe metadata divergence: Fixed
- 3.5 Synthetic machine/operator values: Fixed
- 3.6 Misleading runtimeSeconds label: Fixed
- 3.7 Stats interval mode attribution bug: Fixed
- 3.8 Runtime thread-safety gaps: Fixed
- 3.9 Stop-state and sampling semantics centralization: Fixed

## 4. Database and lifecycle
- 4.1 Relative SQLite path: Fixed
- 4.2 Backup/retention/recovery workflow: Fixed
- 4.3 Multiple server instances unsafe: Fixed
- 4.4 Invalid supplied tag mapping handling: Fixed
- 4.5 Incomplete catalog validation: Fixed
- 4.6 Overlapping config systems: Fixed

## 5. API and server
- 5.1 Backend does not serve production frontend: Fixed
- 5.2 Frontend hardcoded localhost endpoints: Fixed
- 5.3 CORS localhost-only policy for prod: Fixed
- 5.4 Missing health/readiness endpoints: Fixed
- 5.5 Inconsistent error handling / Problem Details: Fixed
- 5.6 Weak server-side line validation: Fixed
- 5.7 Hardcoded status endpoint: Fixed
- 5.8 Missing structured operational logging: Fixed
- 5.9 Shutdown/runtime assertion handling: Fixed

## 6. Frontend and live updates
- 6.1 SignalR-driven refresh storm: Fixed
- 6.2 Startup auth not validated: Fixed
- 6.3 Role behavior inconsistency: Fixed
- 6.4 New-line auto-map stale state: Fixed
- 6.5 Report query incompleteness/pagination: Fixed
- 6.6 CSV formula injection: Fixed
- 6.7 Browser-only settings persistence intent: Fixed
- 6.8 Frontend dependency advisories: Blocked (with evidence)

## 7. Installer, service, and LAN deployment
- 7.1 No production service: Blocked (with evidence)
- 7.2 Export installer attempt incomplete: Blocked (with evidence)
- 7.3 No stable LAN identity model: Blocked (with evidence)
- 7.4 No full installer lifecycle: Blocked (with evidence)
- 7.5 Empty deployment placeholders: Blocked (with evidence)

## 8. Repository, testing, CI, and docs
- 8.1 Generated dependencies/binaries in source package: Fixed
- 8.2 CI doc mismatch/reality: Fixed
- 8.3 Backend validation not demonstrated in prior audit: Fixed
- 8.4 Documentation stale/contradictory: Fixed
- 8.5 README encoding and license anomaly: Fixed

## Evidence log
- Baseline build: `dotnet build backend/backend.csproj -c Release` passed.
- Baseline backend tests: `dotnet test backend.Tests/backend.Tests.csproj -c Release` failed (1 test) due non-idempotent PLC preset seeding unique key conflict.
- Baseline frontend: `npm run lint`, `npm run test:run`, `npm run build` passed after clearing an esbuild lock process.
- Security/auth remediation validation: `dotnet test backend.Tests/backend.Tests.csproj -c Release` passed (27/27).
- Frontend auth transport remediation validation: `npm run lint`, `npm run test:run`, and `npm run build` passed.
- Runtime/report pagination remediation validation: `dotnet test backend.Tests/backend.Tests.csproj -c Release` passed (29/29) after adding paged production-runs/runtime-events contract and query validation tests.
- Runtime/report pagination frontend validation: `npm run test:run` and `npm run build` passed after updating reports and completed-run details pages to paged APIs.
- Health endpoint remediation validation: `dotnet test backend.Tests/backend.Tests.csproj -c Release` passed (31/31) with new `/health/live` and `/health/ready` tests.
- DB path/instance-guard/logging validation: `dotnet test backend.Tests/backend.Tests.csproj -c Release` passed (31/31) after startup updates.
- Single-instance guard verification: launching a second process with `dotnet backend.dll` returned `InvalidOperationException: Another PlantMonitor instance is already running for database ...` and exited with code 1.
- Frontend shell hosting validation: `dotnet test backend.Tests/backend.Tests.csproj -c Release` passed (32/32) with `GET /` integration test asserting `200` and `text/html`.
- PLC/API validation hardening: `dotnet test backend.Tests/backend.Tests.csproj -c Release` passed (34/34) after adding non-200 PLC connection failure semantics (`502`), unknown logical key rejection, and runtime loop per-tick exception containment.
- Production CORS policy hardening: added `backend/appsettings.Production.json` with `App:CorsOrigins` and switched production middleware to `FrontendProd` CORS policy.
- SignalR refresh storm mitigation validation: `npm run test:run` and `npm run build` passed after adding refresh coalescing/throttling in dashboard realtime handlers.
- Documentation alignment remediation: refreshed `docs/Architecture.md` and `docs/Deployment.md` to match current runtime polling + SQLite + backend-hosted frontend deployment model.
- README/license remediation: converted `README.md` from UTF-16 to UTF-8 and added explicit license file at `LICENSE/LICENSE.txt`.
- Backup/restore remediation: executed `scripts/db-backup.ps1` and `scripts/db-restore.ps1` successfully against local sqlite file (`Backup created ...`, `Database restored ...`).
- CI alignment remediation: updated `.github/workflows/ci.yml` to run across push branches and include frontend lint step; local `npm run lint`, `npm run test:run`, and `npm run build` all passed.
- Frontend advisory status: `npm audit --omit=dev` still reports `react-router` high severity advisory (GHSA-qwww-vcr4-c8h2) for current compatible major line; `npm audit fix --force` requires a breaking change path and follow-up route regression test cycle.
- HTTPS production guardrail: backend now enforces `UseHsts()` + `UseHttpsRedirection()` outside Development and uses environment-specific production CORS policy.
- Runtime duration semantics: `runtimeSeconds` now derives from run start metadata instead of status transition timestamp.
- AB commissioning constraints: unresolved items 2.3/2.4 require live Allen-Bradley hardware verification for routed path assumptions and structured tag behavior under production PLC firmware.
- Installer/service/LAN constraints: section 7 requires administrator-level service install operations, installer toolchain artifacts not present in this workspace, and second-machine LAN validation outside the current execution environment.
- Runtime state persistence remediation: added persisted per-line checkpoints (`active_line_runtime_states`) and startup restore to avoid memory-only active production state behavior after service restart.
- Runtime metadata source-of-truth remediation: line configuration now carries explicit `RecipeId`, `MachineId`, and `OperatorName` fields consumed directly by runtime snapshots and completed runs (removed synthetic line-number-derived values).
- Mode attribution/thread-safety remediation: fixed per-sample auto/manual stats attribution and tightened lock-scoped lifecycle flag transitions in runtime loop.
- Frontend role/admin/settings remediation: updated protected-route behavior for operator forbidden flow, fixed administration selected-line stale connection/mapping state resets, and clarified/scoped settings persistence to browser-local signed-in user storage.
- Final validation run: `dotnet test backend.Tests/backend.Tests.csproj -c Release` passed (34/34); `npm run lint`, `npm run test:run`, and `npm run build` passed.
