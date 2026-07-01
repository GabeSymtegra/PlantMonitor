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
- Faulted
- Offline
- Maintenance

Allowed controlMode values:

- Auto
- Manual

## 3. Authentication And Authorization

Auth method:

- JWT Bearer token in Authorization header.

Roles:

- Viewer: Read dashboard and reports.
- Operator: Read dashboard and reports.
- Admin: Read and manage line and tag configuration.

Endpoint policy summary:

- GET endpoints: Viewer, Operator, Admin.
- POST/PUT/DELETE configuration endpoints: Admin only.

## 4. Error Contract

All non-2xx responses use this envelope:

{
	"traceId": "string",
	"code": "string",
	"message": "string",
	"details": {
		"fieldName": ["error1", "error2"]
	}
}

Rules:

- details is optional and used for validation errors.
- code examples: ValidationError, Unauthorized, Forbidden, NotFound, ServerError.

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
	"isActive": true,
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
	"accessToken": "jwt",
	"expiresAtUtc": "2026-07-01T20:00:00Z",
	"user": {
		"id": 1,
		"username": "admin",
		"role": "Admin",
		"displayName": "Plant Admin"
	}
}

Response codes:

- 200, 400, 401, 500

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

