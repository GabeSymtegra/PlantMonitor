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