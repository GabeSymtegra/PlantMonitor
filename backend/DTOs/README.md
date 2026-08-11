# Backend DTOs

DTOs define the backend API contracts and runtime payload shapes.

## Main Groups

- `Authentication/`: login request and response payloads
- `Plc/`: PLC configuration, browsing, reading, validation, and connection DTOs
- `Production/`: completed run and runtime event report payloads
- `Runtime/`: dashboard and line detail snapshots

Keep DTOs focused on transport contracts. Business logic belongs in services.