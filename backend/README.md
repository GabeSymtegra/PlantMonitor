# Backend

The backend is an ASP.NET Core application that owns authentication, live runtime calculation, PLC integration, reporting data, and persistence.

## Key Files And Folders

- `Program.cs`: composition root, API endpoints, authentication, and startup wiring
- `Configuration/`: options objects and configuration binding types
- `Controllers/`: controller-based endpoints, mainly administration and line flows
- `Data/`: EF Core DbContext, migrations, and seeders
- `DTOs/`: request and response contracts
- `Interfaces/`: service abstractions
- `Models/`: persistence entities and domain data models
- `Services/`: implementation layer for auth, PLC, runtime, reporting, and line logic

## Sub Guides

- [Data/README.md](Data/README.md)
- [DTOs/README.md](DTOs/README.md)
- [Models/README.md](Models/README.md)
- [Services/README.md](Services/README.md)
- [Controllers/README.md](Controllers/README.md)
- [Interfaces/README.md](Interfaces/README.md)

## Running The Backend

Preferred startup from repository root:

```powershell
.\scripts\dev.ps1
```

For isolated Siemens testing from repository root:

```powershell
.\scripts\dev.ps1 -Isolated
```

Default local development run:

```powershell
Set-Location backend
dotnet run --launch-profile http
```

Default URL:

- `http://localhost:5265`

If `5265` is already in use or the shared SQLite database is locked by another
PlantMonitor instance, use an isolated backend instance instead:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5266'
$env:ConnectionStrings__PlantMonitor='Data Source=C:\Users\Gabe\Desktop\PlantMonitor\backend\plantmonitor.dev.db'
dotnet run --no-launch-profile --project C:\Users\Gabe\Desktop\PlantMonitor\backend\backend.csproj
```

This isolated mode is the recommended path for Siemens integration testing.