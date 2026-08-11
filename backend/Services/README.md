# Backend Services

Services contain the application behavior of the backend.

## Main Groups

- `Authentication/`: token generation and auth helpers
- `Line/`: line-focused workflows
- `Plc/`: PLC drivers, validation, browsing, and connection logic
- `Product/`: product-oriented services
- `Production/`: runtime engine, dashboard snapshots, lifecycle handling, and reports

## Reading Order

If you are new to the backend, start here:

1. `Program.cs`
2. `Services/Production/ProductionRuntimeService.cs`
3. `Services/Plc/`
4. `Data/PlantMonitorDbContext.cs`