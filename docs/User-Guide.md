# User Guide

## Overview

PlantMonitor helps teams monitor production lines, review live status, inspect line details, administer PLC mappings, and review historical reports.

## Sign In

1. Open the frontend application.
2. Sign in with a role-appropriate account.
3. Use an Admin account for administration and settings.

Development accounts:

- Admin: `test` / `test`
- Operator: `operator` / `test`
- Viewer: `viewer` / `test`

## Dashboard

The dashboard is the live operational view.

- Each line shows status, control mode, runtime, production length, and variance metrics.
- If the backend cannot communicate with the PLC, the line shows `Offline`.
- Time in status resets when the status changes.

## Status Board

The status board is the simplified monitoring screen.

- Use it for wallboard-style viewing.
- It emphasizes current line status and timing without administration controls.

## Administration

Administration is for configuration work.

- Add or update line definitions.
- Test PLC connectivity.
- Assign protocol presets.
- Review effective tag mappings.
- Browse PLC tags and validate addresses.

Recommended workflow:

1. Add or confirm the line record.
2. Test connection to the PLC.
3. Assign the correct manufacturer and preset.
4. Validate or override tag addresses if needed.
5. Save configuration and verify the dashboard updates.

## Reports

The Reports page provides historical visibility.

- Completed runs show start time, end time, final status, runtime, length, and auto/manual percentages.
- Runtime events show mode switches and status switches for each line.
- Use the line filter and date range fields to narrow results.
- Export filtered tables to CSV when needed.

## Common Status Meanings

- `Running`: line is actively producing.
- `Stopped`: line is not producing but still reachable.
- `Bleedout`: line is in bleedout state.
- `Startup`: line is in startup state.
- `Faulted`: line is faulted.
- `Maintenance`: line is in maintenance state.
- `Offline`: PLC communication failed or the line is otherwise unreachable.

## Troubleshooting

- If the dashboard shows `Offline`, first test PLC connectivity in Administration.
- If the backend will not start after model changes, add and apply an EF migration.
- If frontend requests fail with authorization errors, sign in again to refresh the token.