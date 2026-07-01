# PlantMonitor Architecture

## 1. Purpose And Scope

PlantMonitor is a private-network manufacturing monitoring platform.

This document defines the backend and frontend architecture for the current implementation phase:

- Mock PLC data source is used for initial feature delivery.
- System is monitor-only in this phase (no remote PLC control commands).
- Realtime dashboard updates are delivered via SignalR from day one.

Out of scope for this version:

- Direct production PLC driver implementation details (future phase).
- Worker and standalone PLC service process design (future phase).

## 2. System Context

Primary users:

- Viewer: Read-only dashboard and reports.
- Operator: Read-only dashboard and reports.
- Admin: Read plus line and tag configuration management.

Primary business outcomes:

- Observe plant line status in near realtime.
- Register and maintain line metadata (line number, PLC family, IP, polling profile).
- Track historical metrics for reporting.

## 3. High-Level Components

### 3.1 Frontend Web App (React + TypeScript + Vite)

Responsibilities:

- User authentication flow and route protection.
- Dashboard rendering in table and card views.
- Search/filter and summary visualization.
- SignalR subscription for live line updates.
- Administrative forms for line and tag configuration.
- Reports views backed by historical query APIs.

Current code anchors:

- Routing shell: frontend/src/routes/AppRoutes.tsx
- Dashboard state: frontend/src/context/DashboardContext.tsx
- Dashboard page: frontend/src/pages/Dashboard.tsx
- Dashboard data service: frontend/src/services/dashboardService.ts
- Types: frontend/src/types/ProductionLine.ts and frontend/src/types/LineStatus.ts

### 3.2 Backend API (ASP.NET Core)

Responsibilities:

- Authentication and authorization.
- REST APIs for dashboard, lines, tag mappings, and reports.
- SignalR hub for realtime event delivery.
- Read/write of current and historical records in PostgreSQL.
- Mock telemetry producer that simulates PLC values.

Current code anchor:

- Minimal baseline: backend/Program.cs

### 3.3 PostgreSQL Data Store

Responsibilities:

- Source of truth for line configuration and tag definitions.
- Persistence of line snapshots and historical aggregates.
- User and role records if identity data is stored locally.

Schema details are specified in docs/Database.md.

## 4. Realtime Data Flow

### 4.1 Initial Dashboard Load

1. User navigates to dashboard route.
2. Frontend requests GET /api/dashboard.
3. Backend returns current line snapshot collection plus lastUpdated timestamp.
4. Frontend renders summary, table/cards, and freshness indicator.

### 4.2 Realtime Update Flow (SignalR)

1. Frontend opens SignalR connection to /hubs/lines after authentication.
2. Backend pushes line status update events as data changes.
3. Frontend merges updates into in-memory dashboard state.
4. UI components rerender from shared normalized state.

### 4.3 Reconnect Behavior

- Client uses automatic reconnect with exponential backoff.
- On reconnect success, client requests a full snapshot refresh from GET /api/dashboard.
- If hub is unavailable, dashboard shows stale indicator and continues periodic snapshot refresh.

## 5. Mock PLC Integration Strategy (Current Phase)

The backend uses an internal mock telemetry provider with deterministic synthetic values.

Requirements:

- Generate valid status transitions using supported status values:
	- Running
	- Stopped
	- Faulted
	- Offline
	- Maintenance
- Increment runtime and totalLength metrics realistically.
- Preserve per-line identity and configured PLC metadata.

Design rule:

- Backend contracts consumed by frontend must remain stable when real AB and Siemens drivers replace mock sources.

## 6. Frontend Architecture Details

### 6.1 Routing

Routes currently present:

- /login
- /
- /lines
- /products
- /reports
- /administration
- /settings

Target behavior:

- Protected routes require valid auth token.
- Unauthorized users are redirected to /login.
- Role-based UI gates hide admin-only actions.

### 6.2 State Management

DashboardContext owns:

- Current dashboard model (lines, lastUpdated)
- Loading state
- View mode (table or cards)
- Realtime subscription lifecycle
- Search and filter state (to be added)

State rules:

- Single source of truth for dashboard lines.
- All dashboard components consume shared context.
- No component-local duplicate copies of line state.

### 6.3 Presentation Components

Core components:

- StatusSummary
- SearchBar
- ViewToggle
- LineTable
- LineCards

Component behavior requirements:

- Summary counts are derived from live data, never hardcoded.
- Both table and cards consume identical filtered line set.
- Last update time is rendered from dashboard.lastUpdated.

## 7. Backend Architecture Details

### 7.1 API Layers

- Controllers: Route handlers and response contracts.
- Services: Business rules and orchestration.
- Data access layer: EF Core repositories or DbContext-driven services.
- SignalR publisher: Broadcast line and dashboard events.

### 7.2 Security Layers

- JWT bearer authentication.
- Role policies:
	- Viewer: read dashboard and reports
	- Operator: read dashboard and reports
	- Admin: read plus write lines/tag mappings

### 7.3 Error Model

Use consistent error payloads:

- Validation failures: 400 with field-level details.
- Unauthorized: 401.
- Forbidden: 403.
- Not found: 404.
- Unexpected server error: 500 with traceId.

## 8. Non-Functional Requirements

- Availability target: dashboard remains usable during temporary hub disconnect.
- Observability: structured logs for auth, API errors, and realtime publish failures.
- Performance:
	- Initial dashboard payload should support at least 200 lines.
	- UI update latency target under 2 seconds for mock stream.
- Time handling: backend stores UTC timestamps, frontend renders localized display.

## 9. Acceptance Criteria

Architecture is considered implementation-ready when:

1. API contracts in docs/Api.md fully cover dashboard, auth, line config, tag config, and reports.
2. Database model in docs/Database.md can persist all fields in the frontend line model.
3. Deployment runbook in docs/Deployment.md can boot frontend and backend locally.
4. Realtime sequence supports connect, update, disconnect, reconnect, and snapshot refresh.
5. Monitor-only constraint is enforced across frontend and backend.

