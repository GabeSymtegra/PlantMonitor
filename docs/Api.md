# PlantMonitor API Specification

## 1. Scope

This API specification covers backend and frontend integration for the current delivery phase:

- Mock PLC data provider.
- Realtime updates through SignalR.
- Monitoring plus configuration management.
- No PLC control commands in this phase.

## 2. Conventions

### 2.1 Base URLs

- HTTP API base: /api
- SignalR hub: /hubs/lines

### 2.2 Content Type

- Request: application/json
- Response: application/json

### 2.3 Time And Units

- All server timestamps are UTC ISO-8601.
- totalLength unit: feet.
- runtime unit: seconds in API contracts.
- Frontend may format runtime for display as HH:mm:ss.

### 2.4 Status Values

Allowed line status values:

- Running
- Stopped
- Startup
- Bleedout
- Faulted
- Offline
- Maintenance

Allowed controlMode values:

- Auto
- Manual

## 3. Authentication And Authorization

Auth method:

- Cookie-based session auth using HttpOnly `pm_auth` token cookie.
- SignalR hub connections may also use `access_token` query values during WebSocket negotiation.

Roles:

- Viewer: Read dashboard and reports.
- Operator: Read dashboard and reports.
- Admin: Read and manage line and tag configuration.

Endpoint policy summary:

- GET endpoints: Viewer, Operator, Admin.
- POST/PUT/DELETE configuration endpoints: Admin only.

## 4. Error Contract

Validation and domain failures use RFC7807 ProblemDetails payloads:

{
	"type": "https://httpstatuses.com/400",
	"title": "Invalid request parameters.",
	"status": 400,
	"detail": "lineId must be a positive integer.",
	"instance": "/api/production-runs",
	"code": "invalid_line_id"
}

Rules:

- `code` is a machine-readable extension for client logic.
- `traceId` may be included for correlation depending on middleware path.
- Authorization failures continue to use standard 401/403 semantics.

## 5. DTO Models

### 5.1 DashboardLineDto

{
	"id": 1,
	"lineNumber": 1,
	"product": "PVC Pipe",
	"status": "Running",
	"controlMode": "Auto",
	"totalLength": 15200,
	"runtimeSeconds": 45810,
	"plcIp": "192.168.1.101",
	"updatedAtUtc": "2026-07-01T14:25:05Z"
}

### 5.2 DashboardSnapshotDto

{
	"lines": [DashboardLineDto],
	"lastUpdatedUtc": "2026-07-01T14:25:05Z"
}

### 5.3 LineConfigDto

{
	"id": 1,
	"lineNumber": 1,
	"displayName": "Line 1",
	"plcFamily": "AllenBradley",
	"plcIp": "192.168.1.101",
	"pollIntervalMs": 2000,
	"isActive": false,
	"lineLifecycleState": "Draft",
	"defaultProduct": "PVC Pipe",
	"createdAtUtc": "2026-07-01T14:00:00Z",
	"updatedAtUtc": "2026-07-01T14:10:00Z"
}

### 5.4 TagMappingDto

{
	"id": 11,
	"lineId": 1,
	"tagKey": "status",
	"plcAddress": "Program:LineData.Status",
	"dataType": "int",
	"scale": 1.0,
	"isRequired": true,
	"updatedAtUtc": "2026-07-01T14:10:00Z"
}

### 5.5 ReportPointDto

{
	"bucketStartUtc": "2026-07-01T14:00:00Z",
	"lineId": 1,
	"runningSeconds": 300,
	"stoppedSeconds": 0,
	"faultedSeconds": 0,
	"offlineSeconds": 0,
	"producedLength": 120
}

## 6. HTTP Endpoints

### 6.1 Authentication

POST /api/auth/login

Request:

{
	"username": "admin",
	"password": "string"
}

Response 200:

{
	"username": "test",
	"role": "Admin",
	"expiresAtUtc": "2026-07-01T20:00:00Z",
	"mustChangePassword": false
}

Response codes:

- 200, 400, 401, 423

### 6.2 Dashboard

GET /api/dashboard

Purpose:

- Returns current line snapshot for dashboard initial load and reconnect refresh.

Response 200:

DashboardSnapshotDto

Response codes:

- 200, 401, 500

GET /api/dashboard/summary

Purpose:

- Returns status totals and latest update timestamp.

Response 200:

{
	"running": 10,
	"stopped": 2,
	"faulted": 1,
	"offline": 0,
	"maintenance": 1,
	"lastUpdatedUtc": "2026-07-01T14:25:05Z"
}

Response codes:

- 200, 401, 500

### 6.3 Line Configuration

GET /api/lines

- Returns all configured lines.

Response 200:

[LineConfigDto]

POST /api/lines

- Creates a new line configuration.
- Admin only.

Request:

{
	"lineNumber": 5,
	"displayName": "Line 5",
	"plcFamily": "Siemens",
	"plcIp": "192.168.1.105",
	"pollIntervalMs": 2000,
	"isActive": true,
	"defaultProduct": "PEX Tubing"
}

Response 201:

LineConfigDto

PUT /api/lines/{id}

- Updates line configuration.
- Admin only.

DELETE /api/lines/{id}

- Deactivates or removes a line based on server policy.
- Admin only.

Response codes for line config endpoints:

- 200, 201, 204, 400, 401, 403, 404, 409, 500

### 6.4 Tag Mappings

GET /api/lines/{id}/tags

- Returns all tag mappings for a line.

Response 200:

[TagMappingDto]

PUT /api/lines/{id}/tags

- Replaces complete tag mapping set for a line.
- Admin only.

Request:

{
	"tags": [
		{
			"tagKey": "status",
			"plcAddress": "DB12.DBW0",
			"dataType": "int",
			"scale": 1.0,
			"isRequired": true
		}
	]
}

Response 200:

[TagMappingDto]

Response codes:

- 200, 400, 401, 403, 404, 500

### 6.5 Reports

GET /api/reports/line-history?lineId=1&fromUtc=2026-07-01T00:00:00Z&toUtc=2026-07-01T23:59:59Z&bucketMinutes=5

Response 200:

{
	"lineId": 1,
	"fromUtc": "2026-07-01T00:00:00Z",
	"toUtc": "2026-07-01T23:59:59Z",
	"bucketMinutes": 5,
	"points": [ReportPointDto]
}

Response codes:

- 200, 400, 401, 500

### 6.6 PLC Protocol Administration (M1)

All endpoints in this section are Admin only.

GET /api/admin/plc/presets?manufacturer=AB

- Returns seeded protocol presets and baseline tag templates.

Response 200:

[
	{
		"manufacturer": "AB",
		"presetName": "BasicStatus",
		"presetVersion": 1,
		"description": "Allen-Bradley baseline telemetry preset.",
		"tags": [
			{
				"tagKey": "status",
				"plcAddress": "Program:LineData.Status",
				"dataType": "int",
				"scale": 1.0,
				"isRequired": true
			}
		]
	}
]

GET /api/admin/lines/{lineId}/protocol-assignment

- Returns configured protocol preset assignment for one line.

Response 200:

{
	"lineId": 101,
	"manufacturer": "AllenBradley",
	"presetName": "BasicStatus",
	"presetVersion": 1,
	"pollIntervalMs": 1500,
	"routePath": "1,0",
	"processorType": "ControlLogix",
	"connectionTimeoutMs": 3000,
	"readTimeoutMs": 3000,
	"retryCount": 1,
	"retryDelayMs": 250,
	"updatedAtUtc": "2026-07-02T15:30:00Z"
}

Response codes:

- 200, 401, 403, 404

PUT /api/admin/lines/{lineId}/protocol-assignment

- Creates or updates protocol assignment for one line.

Request:

{
	"manufacturer": "AllenBradley",
	"presetName": "BasicStatus",
	"presetVersion": 1,
	"pollIntervalMs": 1500,
	"routePath": "1,0",
	"processorType": "ControlLogix",
	"connectionTimeoutMs": 3000,
	"readTimeoutMs": 3000,
	"retryCount": 1,
	"retryDelayMs": 250
}

Response 200:

LineProtocolAssignmentDto

Response codes:

- 200, 400, 401, 403

GET /api/admin/lines/{lineId}/commissioning-check

- Evaluates whether all required logical tag slots are mapped for the line assignment.
- Returns readiness state, missing required keys, and issue messages for commissioning handoff.

Response 200:

{
	"lineId": 101,
	"manufacturer": "AllenBradley",
	"presetName": "BasicStatus",
	"presetVersion": 1,
	"isReady": false,
	"requiredTagCount": 10,
	"mappedRequiredTagCount": 8,
	"missingRequiredTagKeys": ["machine_state", "control_mode"],
	"issues": ["Missing required logical keys: machine_state, control_mode."],
	"checkedAtUtc": "2026-07-29T18:10:00Z"
}

Response codes:

- 200, 401, 403, 404

POST /api/admin/lines/{lineId}/commissioning-activate

- Explicitly promotes a line to `Active` only after a passing commissioning check.
- Returns `409` and keeps the line non-active when readiness checks fail.

Response 200:

{
	"lineId": 101,
	"lineLifecycleState": "Active"
}

Response codes:

- 200, 401, 403, 404, 409

GET /api/admin/lines/{lineId}/effective-tags

- Returns merged preset tags plus line-level overrides.

Response 200:

[EffectiveTagMappingDto]

Response codes:

- 200, 401, 403, 404

POST /api/admin/lines/{lineId}/validate-tags

- Validates payload against required tag keys, allowed data types, and manufacturer address format.

Request:

{
	"manufacturer": "Siemens",
	"pollIntervalMs": 2000,
	"tags": [
		{
			"tagKey": "status",
			"plcAddress": "DB12.DBW0",
			"dataType": "int",
			"scale": 1.0,
			"isRequired": true
		}
	]
}

Response 200:

{
	"isValid": true,
	"issues": []
}

Response 400:

{
	"isValid": false,
	"issues": [
		{
			"field": "tags[0].plcAddress",
			"message": "Siemens addresses must match DBx.DBW0/DBD0/DBX0.0 or M/I/Q area format."
		}
	]
}

PUT /api/admin/lines/{lineId}/tag-overrides

- Replaces line-level override tags for assigned protocol.

Request:

{
	"tags": [
		{
			"tagKey": "product",
			"plcAddress": "Program:LineData.ProductSerial",
			"dataType": "string",
			"scale": 1.0,
			"isRequired": true
		}
	]
}

Response 200:

[EffectiveTagMappingDto]

Response codes:

- 200, 400, 401, 403, 404

## 7. SignalR Contract

Hub route:

- /hubs/lines

Client to server methods:

- SubscribeAllLines()
- SubscribeLine(lineId)
- UnsubscribeLine(lineId)

Server to client events:

- LineUpdated(DashboardLineDto)
- DashboardSummaryUpdated(summaryPayload)
- SnapshotRefreshRequired(reasonPayload)

Event payload rules:

- Payload schema must match corresponding HTTP DTO names and field casing.
- updatedAtUtc is mandatory on all update events.

## 8. Frontend Integration Notes

Mapping rules for existing frontend model:

- runtimeSeconds from API is formatted to runtime display string in UI.
- lastUpdatedUtc maps to DashboardModel.lastUpdated.
- status values must map exactly to frontend LineStatus union values.

API client requirements:

- Include bearer token if authenticated.
- Normalize error envelope into user-facing message model.
- Retry policy for idempotent GET requests only.

## 9. Acceptance Criteria

1. Frontend dashboard can fully render using GET /api/dashboard response only.
2. SignalR LineUpdated events modify visible rows/cards without page refresh.
3. Admin can create, edit, and list line and tag configuration using defined endpoints.
4. Viewer and Operator cannot call write endpoints successfully.
5. Reports endpoint returns bucketed historical points for chart/table rendering.
6. Error envelope is consistent across all non-2xx responses.

