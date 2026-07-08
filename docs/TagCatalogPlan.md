# Tag Catalog Plan

## Goal

Move PlantMonitor from ad hoc PLC browsing toward a fixed, controlled tag catalog model.

The persisted tag catalog will become the source of truth for:
- what tags exist
- which line they belong to
- which driver reads them
- how they are displayed in the UI
- which logical PlantMonitor fields they map to

## Why This Approach

A fixed tag list is simpler and more reliable than generic live browsing.

Benefits:
- works cleanly for both Allen-Bradley and Siemens
- avoids depending on live symbol discovery as the runtime truth
- avoids top-level structured tag read issues for production flows
- allows consistent logical naming across vendors
- makes polling and future snapshots deterministic

## Implementation Phases

### Phase 1: Persistent Tag Catalog

Add a backend-persisted tag catalog model.

Each catalog entry should contain:
- `Id`
- `LineId`
- `LogicalKey`
- `DisplayName`
- `Driver`
- `PlcAddress`
- `DataType`
- `IsReadable`
- `IsWritable`
- `Unit`
- `Scale`
- `Description`
- `IsEnabled`
- `ReadMode`
- `SortOrder`

### Phase 2: Admin Catalog Management

Extend the Administration page to manage the fixed tag catalog.

The admin page should support:
- viewing configured tags for a line
- importing or entering tags
- editing display names and logical keys
- test-reading a configured tag
- showing read status, last value, and error state

### Phase 3: Vendor Read Layer

Use the configured catalog as the production read source.

Allen-Bradley:
- keep live browse as a discovery aid only
- runtime reads should target configured addresses

Siemens:
- use configured addresses directly
- do not depend on live symbol browse

### Phase 4: Logical Mapping Layer

Add logical PlantMonitor fields above vendor tag addresses.

Examples:
- `Machine.Status`
- `Production.LineSpeed`
- `Production.Diameter`
- `Alarm.Code`

### Phase 5: Polling and Snapshot Runtime

Poll configured tags and build logical runtime snapshots.

## Current Code Impact

Backend files to extend:
- `backend/Data/PlantMonitorDbContext.cs`
- `backend/Models/Plc/*`
- `backend/Services/Plc/EfPlcProtocolConfigService.cs`
- `backend/Controllers/Admin/PlcProtocolAdminController.cs`
- `backend/Services/Plc/AllenBradleyPlcDriver.cs`
- `backend/Services/Plc/SiemensPlcDriver.cs`

Frontend files to extend:
- `frontend/src/pages/Administration.tsx`
- `frontend/src/services/plcTagBrowserService.ts`
- `frontend/src/services/plcTagCatalogService.ts`

## Placeholder: Updated Tag List

Paste the real tag list here when you receive it.

### Tag Intake Table

| Line | Driver | LogicalKey | DisplayName | PLC Address | DataType | Unit | Scale | Description | Read Frequency | Required | Notes |
|------|--------|------------|-------------|-------------|----------|------|-------|-------------|----------------|----------|-------|
|      |        |            |             |             |          |      |       |             |                |          |       |
|      |        |            |             |             |          |      |       |             |                |          |       |
|      |        |            |             |             |          |      |       |             |                |          |       |

## Placeholder: Structured Tag Expansion Notes

Use this section for parent/controller symbols that must be converted into actual readable member addresses.

| Parent Tag | Member Path | Final Read Address | Notes |
|------------|-------------|--------------------|-------|
|            |             |                    |       |
|            |             |                    |       |
|            |             |                    |       |