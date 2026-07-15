# Backend Data

This folder contains database-facing code.

## Contents

- `PlantMonitorDbContext.cs`: EF Core model registration and table mapping
- `Migrations/`: schema history
- `PlcPresetSeeder.cs`: baseline PLC preset seed data
- `RecipeToleranceSeeder.cs`: recipe tolerance seed data

## Notes

- New persisted entities require a migration before application startup.
- SQLite is the active local persistence layer.