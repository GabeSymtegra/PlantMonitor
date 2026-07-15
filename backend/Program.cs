using System.Security.Claims;
using System.Text;
using backend.Configuration;
using backend.Data;
using backend.DTOs.Authentication;
using backend.DTOs.Plc;
using backend.Interfaces.Production;
using backend.Interfaces;
using backend.Interfaces.Plc;
using backend.DTOs.Production;
using backend.Services.Line;
using backend.Services.Authentication;
using backend.Services.Plc;
using backend.Services.Production;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// -----------------------------------------------------------------------------
// Application bootstrap
// -----------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// Core configuration and persistence
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

var connectionString = builder.Configuration.GetConnectionString("PlantMonitor")
    ?? "Data Source=plantmonitor.db";

builder.Services.AddDbContext<PlantMonitorDbContext>(options =>
    options.UseSqlite(connectionString));

// Application services
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IPlcTagAddressValidator, PlcTagAddressValidator>();
builder.Services.AddScoped<IPlcProtocolConfigService, EfPlcProtocolConfigService>();
builder.Services.AddSingleton<IPlcDriver, AllenBradleyPlcDriver>();
builder.Services.AddSingleton<IPlcDriver, SiemensPlcDriver>();
builder.Services.AddSingleton<IPlcConnectionService>(serviceProvider =>
    new PlcConnectionService(serviceProvider.GetServices<IPlcDriver>().ToArray()));
builder.Services.AddSingleton<ProductionRuntimeService>();
builder.Services.AddSingleton<IProductionRuntimeService>(serviceProvider =>
    serviceProvider.GetRequiredService<ProductionRuntimeService>());
builder.Services.AddScoped<IRecipeToleranceService, RecipeToleranceService>();
builder.Services.AddControllers();

// Authentication and authorization
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/lines"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Development CORS and realtime wiring
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                return uri.Scheme is "http" or "https"
                    && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                        || uri.Host == "127.0.0.1");
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddSignalR();
builder.Services.AddHostedService<LineUpdateBroadcastService>();
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<ProductionRuntimeService>());

// -----------------------------------------------------------------------------
// Application startup
// -----------------------------------------------------------------------------

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();

    var hasLegacySchemaWithoutMigrations =
        HasSqliteTable(dbContext, "line_protocol_assignments")
        && !HasSqliteTable(dbContext, "__EFMigrationsHistory");

    if (hasLegacySchemaWithoutMigrations)
    {
        dbContext.Database.EnsureCreated();
        EnsureLegacyRuntimeSchema(dbContext);
    }
    else
    {
        try
        {
            dbContext.Database.Migrate();
        }
        catch (SqliteException ex)
            when (ex.SqliteErrorCode == 1
                && ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            // Compatibility and race-safe fallback for legacy local/test SQLite files.
            dbContext.Database.EnsureCreated();
            EnsureLegacyRuntimeSchema(dbContext);
        }
    }

    await PlcPresetSeeder.SeedAsync(dbContext);
    await RecipeToleranceSeeder.SeedAsync(dbContext);
}

// Middleware pipeline
app.UseCors("FrontendDev");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// -----------------------------------------------------------------------------
// Minimal API surface
// -----------------------------------------------------------------------------

app.MapPost("/api/auth/login", (LoginRequestDto request, IJwtTokenService jwtTokenService) =>
{
    var credentials = new Dictionary<string, (string Password, string Role)>(StringComparer.OrdinalIgnoreCase)
    {
        ["test"] = ("test", "Admin"),
        ["operator"] = ("test", "Operator"),
        ["viewer"] = ("test", "Viewer"),
    };

    if (!credentials.TryGetValue(request.Username, out var account) || account.Password != request.Password)
    {
        return Results.Unauthorized();
    }

    var accessToken = jwtTokenService.CreateToken(request.Username, account.Role);

    return Results.Ok(new LoginResponseDto
    {
        AccessToken = accessToken,
        Username = request.Username,
        Role = account.Role,
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(jwtOptions.ExpirationMinutes),
    });
});

app.MapGet("/api/status", () =>
{
    return Results.Json(new[]
    {
        new
        {
            id = 1,
            line = "Line 1",
            status = "Running",
            product = "Pepsi 20oz",
            mode = "Auto",
            length = 14220
        },
        new
        {
            id = 2,
            line = "Line 2",
            status = "Stopped",
            product = "Mountain Dew",
            mode = "Manual",
            length = 8100
        }
    });
}).RequireAuthorization();

app.MapGet("/api/dashboard", (IProductionRuntimeService runtimeService) =>
{
    return Results.Ok(runtimeService.GetDashboardSnapshot());
}).RequireAuthorization();

app.MapGet("/api/lines/{lineId:int}/details", (int lineId, IProductionRuntimeService runtimeService) =>
{
    var detail = runtimeService.GetLineDetail(lineId);
    return detail is null
        ? Results.NotFound(new { message = "Line runtime details were not found." })
        : Results.Ok(detail);
}).RequireAuthorization();

app.MapGet("/api/production-runs", async (
    [AsParameters] ProductionRunsQuery query,
    IProductionRuntimeService runtimeService,
    CancellationToken cancellationToken) =>
{
    var rows = await runtimeService.GetCompletedRunsAsync(query.LineId, query.Take, cancellationToken);
    return Results.Ok(rows);
}).RequireAuthorization();

app.MapGet("/api/reports/events", async (
    [AsParameters] RuntimeEventsQuery query,
    IProductionRuntimeService runtimeService,
    CancellationToken cancellationToken) =>
{
    var rows = await runtimeService.GetRuntimeEventsAsync(query.LineId, query.Take, cancellationToken);
    return Results.Ok(rows);
}).RequireAuthorization();

app.MapGet("/api/recipe-tolerances", (
    [AsParameters] RecipeToleranceQuery query,
    IRecipeToleranceService toleranceService) =>
{
    var rows = toleranceService.GetActiveTolerances(query.RecipeId, query.ProductId);
    return Results.Ok(rows);
}).RequireAuthorization();

app.MapGet("/api/configuration/access-check", (ClaimsPrincipal user) =>
{
    return Results.Ok(new
    {
        Message = "Configuration access granted.",
        Username = user.Identity?.Name,
        Role = user.FindFirstValue(ClaimTypes.Role),
    });
}).RequireAuthorization("AdminOnly");

app.MapHub<LinesHub>("/hubs/lines").RequireAuthorization();

app.Run();

// -----------------------------------------------------------------------------
// Local SQLite compatibility helpers
// -----------------------------------------------------------------------------

static bool HasSqliteTable(PlantMonitorDbContext dbContext, string tableName)
{
    if (!string.Equals(dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
    {
        return false;
    }

    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        connection.Open();
    }

    try
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $tableName LIMIT 1;";

        var tableNameParameter = command.CreateParameter();
        tableNameParameter.ParameterName = "$tableName";
        tableNameParameter.Value = tableName;
        command.Parameters.Add(tableNameParameter);

        var result = command.ExecuteScalar();
        return result is not null;
    }
    finally
    {
        if (shouldClose)
        {
            connection.Close();
        }
    }
}

static void EnsureLegacyRuntimeSchema(PlantMonitorDbContext dbContext)
{
    // Older local databases may exist without EF migration history. These
    // guards keep dev and test environments bootable while the schema evolves.
    if (!string.Equals(dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
    {
        return;
    }

    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "LineNumber", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "LineName", "TEXT NOT NULL DEFAULT ''");
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "ProductId", "TEXT NOT NULL DEFAULT ''");
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "PlcIp", "TEXT NOT NULL DEFAULT ''");
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "IsActive", "INTEGER NOT NULL DEFAULT 1");

    dbContext.Database.ExecuteSqlRaw(@"
UPDATE line_protocol_assignments
SET LineNumber = CASE WHEN LineNumber = 0 THEN LineId ELSE LineNumber END,
    LineName = CASE WHEN trim(coalesce(LineName, '')) = '' THEN 'Line ' || LineId ELSE LineName END,
    IsActive = CASE WHEN IsActive = 0 THEN 1 ELSE IsActive END;
");

    if (!HasSqliteTable(dbContext, "recipe_tolerances"))
    {
        dbContext.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS recipe_tolerances (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    RecipeId TEXT NOT NULL,
    ProductId TEXT NOT NULL,
    MeasurementType TEXT NOT NULL,
    TargetValue TEXT NOT NULL,
    ToleranceMinus TEXT NOT NULL,
    TolerancePlus TEXT NOT NULL,
    Version INTEGER NOT NULL,
    IsActive INTEGER NOT NULL,
    Source TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_recipe_tolerances_RecipeId_MeasurementType_Version
    ON recipe_tolerances (RecipeId, MeasurementType, Version);
");
    }

    if (!HasSqliteTable(dbContext, "completed_production_runs"))
    {
        dbContext.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS completed_production_runs (
    Id TEXT NOT NULL PRIMARY KEY,
    LineId INTEGER NOT NULL,
    LineNumber INTEGER NOT NULL,
    LineName TEXT NOT NULL,
    ProductId TEXT NOT NULL,
    RecipeId TEXT NOT NULL,
    MachineId TEXT NOT NULL,
    OperatorName TEXT NOT NULL,
    Manufacturer TEXT NOT NULL,
    PlcIp TEXT NOT NULL,
    FinalStatus TEXT NOT NULL,
    StartTimeUtc TEXT NOT NULL,
    EndTimeUtc TEXT NOT NULL,
    RuntimeSeconds REAL NOT NULL,
    ProductionLength REAL NOT NULL,
    AutoTimeSeconds REAL NOT NULL,
    ManualTimeSeconds REAL NOT NULL,
    AutoPercentage REAL NOT NULL,
    ManualPercentage REAL NOT NULL,
    CreatedAtUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_completed_production_runs_EndTimeUtc
    ON completed_production_runs (EndTimeUtc);
CREATE INDEX IF NOT EXISTS IX_completed_production_runs_LineId
    ON completed_production_runs (LineId);
");
    }

    if (!HasSqliteTable(dbContext, "completed_production_run_zone_stats"))
    {
        dbContext.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS completed_production_run_zone_stats (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    RunId TEXT NOT NULL,
    Zone TEXT NOT NULL,
    Segment TEXT NOT NULL,
    MeasurementCount INTEGER NOT NULL,
    SkippedCount INTEGER NOT NULL,
    RunningAbsoluteDeviationSum REAL NOT NULL,
    AverageAbsoluteDeviation REAL NOT NULL,
    MaxPositiveDeviation REAL NOT NULL,
    MaxNegativeDeviation REAL NOT NULL,
    CurrentDeviation REAL NOT NULL,
    FOREIGN KEY (RunId) REFERENCES completed_production_runs (Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_completed_production_run_zone_stats_RunId_Zone_Segment
    ON completed_production_run_zone_stats (RunId, Zone, Segment);
");
    }

    if (!HasSqliteTable(dbContext, "runtime_events"))
    {
        dbContext.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS runtime_events (
    Id TEXT NOT NULL PRIMARY KEY,
    LineId INTEGER NOT NULL,
    LineNumber INTEGER NOT NULL,
    LineName TEXT NOT NULL,
    EventType TEXT NOT NULL,
    PreviousValue TEXT NOT NULL,
    CurrentValue TEXT NOT NULL,
    OccurredAtUtc TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_runtime_events_LineId
    ON runtime_events (LineId);
CREATE INDEX IF NOT EXISTS IX_runtime_events_OccurredAtUtc
    ON runtime_events (OccurredAtUtc);
");
    }
}

static void EnsureSqliteColumn(PlantMonitorDbContext dbContext, string tableName, string columnName, string columnDefinition)
{
    if (!HasSqliteTable(dbContext, tableName) || HasSqliteColumn(dbContext, tableName, columnName))
    {
        return;
    }

    dbContext.Database.ExecuteSqlRaw($"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition};");
}

static bool HasSqliteColumn(PlantMonitorDbContext dbContext, string tableName, string columnName)
{
    if (!string.Equals(dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
    {
        return false;
    }

    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        connection.Open();
    }

    try
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            if (name.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    finally
    {
        if (shouldClose)
        {
            connection.Close();
        }
    }
}

public partial class Program;

// Query DTOs used by minimal APIs
public sealed class ProductionRunsQuery
{
    public int? LineId { get; init; }
    public int Take { get; init; } = 50;
}

public sealed class RecipeToleranceQuery
{
    public string? RecipeId { get; init; }
    public string? ProductId { get; init; }
}

public sealed class RuntimeEventsQuery
{
    public int? LineId { get; init; }
    public int Take { get; init; } = 100;
}