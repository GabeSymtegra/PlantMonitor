# PlantMonitor Database Specification

## 1. Scope

This document defines the PostgreSQL data model for backend and frontend requirements in the current phase:

- Realtime monitoring with mock PLC data.
- Line and tag configuration management.
- Historical data for reports.
- Authentication and role authorization support.

## 2. Design Principles

- Store timestamps in UTC.
- Preserve stable IDs for lines and mappings.
- Separate configuration entities from telemetry entities.
- Support high-write historical inserts with predictable query patterns.
- Keep schema compatible when mock source is replaced by real PLC drivers.

## 3. Logical Entity Overview

Configuration entities:

- users
- roles
- user_roles
- lines
- line_tag_mappings

Telemetry entities:

- line_snapshots_current
- line_snapshots_history
- line_status_transitions

Optional reporting acceleration entity:

- line_metric_buckets

## 4. Schema Definition

### 4.1 roles

Columns:

- id bigserial primary key
- name varchar(50) not null unique
- description varchar(200) null
- created_at_utc timestamptz not null default now()

Seed values:

- Viewer
- Operator
- Admin

### 4.2 users

Columns:

- id bigserial primary key
- username varchar(100) not null unique
- password_hash varchar(500) not null
- display_name varchar(150) not null
- is_active boolean not null default true
- created_at_utc timestamptz not null default now()
- updated_at_utc timestamptz not null default now()

### 4.3 user_roles

Columns:

- user_id bigint not null references users(id)
- role_id bigint not null references roles(id)
- assigned_at_utc timestamptz not null default now()

Primary key:

- (user_id, role_id)

### 4.4 lines

Columns:

- id bigserial primary key
- line_number int not null unique
- display_name varchar(120) not null
- plc_family varchar(40) not null
- plc_ip inet not null
- poll_interval_ms int not null
- is_active boolean not null default true
- default_product varchar(120) null
- created_at_utc timestamptz not null default now()
- updated_at_utc timestamptz not null default now()

Constraints:

- poll_interval_ms >= 500
- plc_family in (AllenBradley, Siemens)

### 4.5 line_tag_mappings

Columns:

- id bigserial primary key
- line_id bigint not null references lines(id) on delete cascade
- tag_key varchar(80) not null
- plc_address varchar(200) not null
- data_type varchar(40) not null
- scale numeric(18,6) not null default 1.0
- is_required boolean not null default true
- created_at_utc timestamptz not null default now()
- updated_at_utc timestamptz not null default now()

Unique constraints:

- (line_id, tag_key)

Required tag_key minimum set for dashboard:

- status
- product
- runtime_seconds
- total_length
- control_mode

### 4.6 line_snapshots_current

Purpose:

- Fast read model for current dashboard and summary endpoints.

Columns:

- line_id bigint primary key references lines(id) on delete cascade
- product varchar(120) not null
- status varchar(40) not null
- control_mode varchar(20) not null
- runtime_seconds bigint not null
- total_length numeric(18,3) not null
- source_timestamp_utc timestamptz not null
- updated_at_utc timestamptz not null default now()

Constraints:

- status in (Running, Stopped, Faulted, Offline, Maintenance)
- control_mode in (Auto, Manual)
- runtime_seconds >= 0
- total_length >= 0

### 4.7 line_snapshots_history

Purpose:

- Append-only telemetry history for reports.

Columns:

- id bigserial primary key
- line_id bigint not null references lines(id) on delete cascade
- product varchar(120) not null
- status varchar(40) not null
- control_mode varchar(20) not null
- runtime_seconds bigint not null
- total_length numeric(18,3) not null
- source_timestamp_utc timestamptz not null
- ingested_at_utc timestamptz not null default now()

Recommended partitioning:

- Range partition by month on source_timestamp_utc.

### 4.8 line_status_transitions

Purpose:

- High-fidelity status duration analytics.

Columns:

- id bigserial primary key
- line_id bigint not null references lines(id) on delete cascade
- from_status varchar(40) not null
- to_status varchar(40) not null
- changed_at_utc timestamptz not null
- captured_at_utc timestamptz not null default now()

### 4.9 line_metric_buckets (optional)

Purpose:

- Pre-aggregated reporting buckets to reduce query cost.

Columns:

- id bigserial primary key
- line_id bigint not null references lines(id) on delete cascade
- bucket_start_utc timestamptz not null
- bucket_minutes int not null
- running_seconds int not null default 0
- stopped_seconds int not null default 0
- faulted_seconds int not null default 0
- offline_seconds int not null default 0
- maintenance_seconds int not null default 0
- produced_length numeric(18,3) not null default 0
- created_at_utc timestamptz not null default now()

Unique constraints:

- (line_id, bucket_start_utc, bucket_minutes)

## 5. Indexing Strategy

Required indexes:

- lines(is_active)
- line_tag_mappings(line_id)
- line_snapshots_current(status)
- line_snapshots_history(line_id, source_timestamp_utc desc)
- line_status_transitions(line_id, changed_at_utc desc)

If using partitions, also ensure each partition has:

- (line_id, source_timestamp_utc)

## 6. Data Retention

Recommended initial policy:

- Keep line_snapshots_history for 90 days.
- Keep line_status_transitions for 180 days.
- Keep line_metric_buckets for 365 days.

Retention tasks:

- Daily cleanup job deletes expired partition ranges.
- Aggregated bucket data should be generated before raw history expiration.

## 7. Migration Conventions

- Use EF Core migrations in backend project.
- One migration per logical schema change.
- Migration names should include date and purpose.
- Never modify an applied migration file.
- For breaking changes, create additive migration plus backfill script.

Example naming:

- 20260701_InitialCoreSchema
- 20260705_AddLineMetricBuckets

## 8. Seed Data Requirements

Minimum seed records for local development:

- Roles: Viewer, Operator, Admin.
- One admin user.
- At least four lines matching mock dashboard line numbers and IP addresses.
- Required tag mappings for each seeded line.
- Initial line_snapshots_current rows for seeded lines.

## 9. Query Contracts For API

Dashboard endpoint query source:

- Select from line_snapshots_current joined to lines where lines.is_active = true.

Summary endpoint:

- Group line_snapshots_current by status.

Report endpoint:

- Prefer line_metric_buckets when bucket size matches.
- Fallback to line_snapshots_history aggregation when no bucket rows exist.

## 10. Acceptance Criteria

1. Schema supports all frontend line fields: id, lineNumber, product, status, controlMode, totalLength, runtime, plcIp.
2. All write paths for line config and tag mapping are referentially safe.
3. Dashboard current snapshot query executes without full table scans at expected line counts.
4. Historical data can be queried by line and time range for reports.
5. Retention policy is implementable through repeatable scheduled jobs.
6. Schema does not require contract changes when mock telemetry source is replaced.

