# Backend Models

This folder contains persistence-oriented entities and supporting domain models.

## Main Groups

- `Plc/`: configuration entities for presets, assignments, overrides, and catalog entries
- `Production/`: completed run, runtime event, and production reporting entities

These types are mapped by `PlantMonitorDbContext` and should stay aligned with migrations.