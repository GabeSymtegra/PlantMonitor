using System.Security.Claims;
using System.Text;
using backend.Configuration;
using backend.Data;
using backend.DTOs.Authentication;
using backend.DTOs.Plc;
using backend.Interfaces.Production;
using backend.Interfaces;
using backend.Interfaces.Authentication;
using backend.Interfaces.Plc;
using backend.DTOs.Production;
using backend.Models.Authentication;
using backend.Services.Line;
using backend.Services.Authentication;
using backend.Services.Plc;
using backend.Services.Production;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

// -----------------------------------------------------------------------------
// Application bootstrap
// -----------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// Core configuration and persistence
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.SigningKey = ResolveJwtSigningKey(builder.Configuration, builder.Environment, jwtOptions.SigningKey);
builder.Services.PostConfigure<JwtOptions>(options =>
{
    options.SigningKey = jwtOptions.SigningKey;
});

var connectionString = builder.Configuration.GetConnectionString("PlantMonitor")
    ?? "Data Source=plantmonitor.db";
connectionString = ResolvePlantMonitorConnectionString(connectionString, builder.Environment);

builder.Services.AddDbContext<PlantMonitorDbContext>(options =>
    options.UseSqlite(connectionString));

// Application services
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ILocalUserAuthService, LocalUserAuthService>();
builder.Services.AddScoped<IPasswordHasher<LocalUserEntity>, PasswordHasher<LocalUserEntity>>();
builder.Services.AddSingleton<IPlcTagAddressValidator, PlcTagAddressValidator>();
builder.Services.AddScoped<IPlcProtocolConfigService, EfPlcProtocolConfigService>();
builder.Services.AddSingleton<IPlcDriver, AllenBradleyPlcDriver>();
builder.Services.AddSingleton<IPlcDriver, SiemensPlcDriver>();
builder.Services.AddSingleton<IPlcConnectionService>(serviceProvider =>
    new PlcConnectionService(serviceProvider.GetServices<IPlcDriver>().ToArray()));
builder.Services.AddSingleton<ProductionRuntimeService>();
builder.Services.AddSingleton<IProductionRuntimeService>(serviceProvider =>
    serviceProvider.GetRequiredService<ProductionRuntimeService>());
builder.Services.AddSingleton<IProductionReportsService, ProductionReportsService>();
builder.Services.AddScoped<IRecipeToleranceService, RecipeToleranceService>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("LoginLimiter", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            }));
});

// Authentication and authorization
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers.Authorization.ToString();
                var hasBearerHeader = !string.IsNullOrWhiteSpace(authHeader)
                    && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

                if (!hasBearerHeader)
                {
                    var cookieToken = context.Request.Cookies["pm_auth"];
                    if (!string.IsNullOrWhiteSpace(cookieToken))
                    {
                        context.Token = cookieToken;
                    }
                }

                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (string.IsNullOrWhiteSpace(context.Token)
                    && !string.IsNullOrEmpty(accessToken)
                    && path.StartsWithSegments("/hubs/lines"))
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

    options.AddPolicy("FrontendProd", policy =>
    {
        var configuredOrigins = builder.Configuration
            .GetSection("App:CorsOrigins")
            .Get<string[]>()
            ?? [];

        var allowedOrigins = configuredOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
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
var instanceLock = AcquireSingleInstanceLock(connectionString);
app.Lifetime.ApplicationStopping.Register(instanceLock.Dispose);

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

    var authService = scope.ServiceProvider.GetRequiredService<ILocalUserAuthService>();
    await authService.EnsureBootstrapUsersAsync();
}

// Middleware pipeline
app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    var correlationId = context.TraceIdentifier;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; connect-src 'self' ws: wss:; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; frame-ancestors 'none';";
    await next();
});

app.Use(async (context, next) =>
{
    var startedAt = DateTime.UtcNow;
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    await next();
    stopwatch.Stop();

    app.Logger.LogInformation(
        "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms correlationId={CorrelationId} remoteIp={RemoteIp} startedAtUtc={StartedAtUtc}",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        stopwatch.ElapsedMilliseconds,
        context.TraceIdentifier,
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        startedAt);
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.UseCors("FrontendDev");
}
else
{
    app.UseCors("FrontendProd");
}
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true
        && context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && !context.Request.Path.StartsWithSegments("/api/auth/session", StringComparison.OrdinalIgnoreCase)
        && !context.Request.Path.StartsWithSegments("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
        && !context.Request.Path.StartsWithSegments("/api/auth/change-password", StringComparison.OrdinalIgnoreCase))
    {
        var username = context.User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(username))
        {
            var authService = context.RequestServices.GetRequiredService<ILocalUserAuthService>();
            var account = await authService.GetByUsernameAsync(username, context.RequestAborted);
            if (account is not null && account.MustChangePassword)
            {
                var problem = new ProblemDetails
                {
                    Title = "Password change required",
                    Detail = "You must change your password before using this endpoint.",
                    Status = StatusCodes.Status403Forbidden,
                    Type = "https://httpstatuses.com/403",
                    Instance = context.Request.Path,
                };

                problem.Extensions["traceId"] = context.TraceIdentifier;
                problem.Extensions["requiredAction"] = "change-password";

                await Results.Json(problem, statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(context);
                return;
            }
        }
    }

    await next();
});

app.UseAuthorization();
app.MapControllers();

// -----------------------------------------------------------------------------
// Minimal API surface
// -----------------------------------------------------------------------------

var loginEndpoint = app.MapPost("/api/auth/login", async (
    LoginRequestDto request,
    HttpContext httpContext,
    ILocalUserAuthService localUserAuthService,
    IJwtTokenService jwtTokenService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return BadRequestProblem("Username and password are required.", "missing_credentials");
    }

    var auth = await localUserAuthService.AuthenticateAsync(request.Username, request.Password, cancellationToken);
    if (!auth.Success || auth.User is null)
    {
        if (auth.IsLockedOut)
        {
            return LockedProblem("Account is temporarily locked due to repeated failed login attempts.", "account_locked");
        }

        return Results.Unauthorized();
    }

    var accessToken = jwtTokenService.CreateToken(auth.User.Username, auth.User.Role);
    var expiresAt = DateTime.UtcNow.AddMinutes(jwtOptions.ExpirationMinutes);

    httpContext.Response.Cookies.Append("pm_auth", accessToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = !app.Environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Expires = request.RememberMe ? expiresAt : null,
        IsEssential = true,
        Path = "/",
    });

    return Results.Ok(new LoginResponseDto
    {
        Username = auth.User.Username,
        Role = auth.User.Role,
        ExpiresAtUtc = expiresAt,
        MustChangePassword = auth.User.MustChangePassword,
    });
});

var disableLoginRateLimiter = builder.Configuration.GetValue<bool>("App:DisableLoginRateLimiter");
if (!disableLoginRateLimiter)
{
    loginEndpoint.RequireRateLimiting("LoginLimiter");
}

app.MapPost("/api/auth/logout", (HttpContext httpContext) =>
{
    httpContext.Response.Cookies.Delete("pm_auth", new CookieOptions { Path = "/" });
    return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/api/auth/session", async (
    ClaimsPrincipal user,
    ILocalUserAuthService localUserAuthService,
    CancellationToken cancellationToken) =>
{
    var username = user.Identity?.Name;
    if (string.IsNullOrWhiteSpace(username))
    {
        return Results.Unauthorized();
    }

    var account = await localUserAuthService.GetByUsernameAsync(username, cancellationToken);
    if (account is null || account.IsDisabled)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new LoginResponseDto
    {
        Username = account.Username,
        Role = account.Role,
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(jwtOptions.ExpirationMinutes),
        MustChangePassword = account.MustChangePassword,
    });
}).RequireAuthorization();

app.MapPost("/api/auth/change-password", async (
    ClaimsPrincipal user,
    ChangePasswordRequestDto request,
    ILocalUserAuthService localUserAuthService,
    CancellationToken cancellationToken) =>
{
    var username = user.Identity?.Name;
    if (string.IsNullOrWhiteSpace(username))
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12)
    {
        return BadRequestProblem("New password must be at least 12 characters long.", "weak_password");
    }

    var changed = await localUserAuthService.ChangePasswordAsync(
        username,
        request.CurrentPassword,
        request.NewPassword,
        cancellationToken);

    return changed
        ? Results.NoContent()
        : BadRequestProblem("Password change failed.", "password_change_failed");
}).RequireAuthorization();

app.MapGet("/api/status", (IProductionRuntimeService runtimeService) =>
{
    var snapshot = runtimeService.GetDashboardSnapshot();
    return Results.Ok(snapshot.Lines);
}).RequireAuthorization();

app.MapGet("/api/dashboard", (IProductionRuntimeService runtimeService) =>
{
    return Results.Ok(runtimeService.GetDashboardSnapshot());
}).RequireAuthorization();

app.MapGet("/api/lines/{lineId:int}/details", (int lineId, IProductionRuntimeService runtimeService) =>
{
    var detail = runtimeService.GetLineDetail(lineId);
    return detail is null
    ? NotFoundProblem("Line runtime details were not found.", "line_runtime_not_found")
        : Results.Ok(detail);
}).RequireAuthorization();

app.MapGet("/api/production-runs", async (
    [AsParameters] ProductionRunsQuery query,
    PlantMonitorDbContext dbContext,
    IProductionReportsService reportsService,
    CancellationToken cancellationToken) =>
{
    if (query.LineId is <= 0)
    {
        return BadRequestProblem("lineId must be a positive integer.", "invalid_line_id");
    }

    if (query.LineId.HasValue)
    {
        var lineExists = await dbContext.LineProtocolAssignments
            .AsNoTracking()
            .AnyAsync(x => x.LineId == query.LineId.Value, cancellationToken);

        if (!lineExists)
        {
            return BadRequestProblem("lineId was not found.", "line_not_found");
        }
    }

    var fromUtc = query.FromDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    var toUtc = query.ToDate?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
    if (fromUtc.HasValue && toUtc.HasValue && toUtc.Value < fromUtc.Value)
    {
        return BadRequestProblem("toDate must be on or after fromDate.", "invalid_date_range");
    }

    var (skip, take) = ResolvePagination(query.Page, query.PageSize, query.Skip, query.Take, 50, 500);
    var rows = await reportsService.GetCompletedRunsAsync(query.LineId, fromUtc, toUtc, skip, take, cancellationToken);
    return Results.Ok(rows);
}).RequireAuthorization();

app.MapGet("/api/production-runs/{runId:guid}", async (
    Guid runId,
    IProductionReportsService reportsService,
    CancellationToken cancellationToken) =>
{
    var run = await reportsService.GetCompletedRunAsync(runId, cancellationToken);
    return run is null
    ? NotFoundProblem("Completed production run was not found.", "production_run_not_found")
        : Results.Ok(run);
}).RequireAuthorization();

app.MapDelete("/api/production-runs/{runId:guid}", async (
    Guid runId,
    ClaimsPrincipal user,
    IProductionReportsService reportsService,
    CancellationToken cancellationToken) =>
{
    var username = user.Identity?.Name ?? "unknown";
    var role = user.FindFirstValue(ClaimTypes.Role) ?? "unknown";

    var deleted = await reportsService.DeleteCompletedRunAsync(runId, username, role, cancellationToken);
    return deleted
        ? Results.NoContent()
        : NotFoundProblem("Completed production run was not found.", "production_run_not_found");
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/reports/events", async (
    [AsParameters] RuntimeEventsQuery query,
    PlantMonitorDbContext dbContext,
    IProductionReportsService reportsService,
    CancellationToken cancellationToken) =>
{
    if (query.LineId is <= 0)
    {
        return BadRequestProblem("lineId must be a positive integer.", "invalid_line_id");
    }

    if (query.LineId.HasValue)
    {
        var lineExists = await dbContext.LineProtocolAssignments
            .AsNoTracking()
            .AnyAsync(x => x.LineId == query.LineId.Value, cancellationToken);

        if (!lineExists)
        {
            return BadRequestProblem("lineId was not found.", "line_not_found");
        }
    }

    var fromUtc = query.FromDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    var toUtc = query.ToDate?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
    if (fromUtc.HasValue && toUtc.HasValue && toUtc.Value < fromUtc.Value)
    {
        return BadRequestProblem("toDate must be on or after fromDate.", "invalid_date_range");
    }

    var (skip, take) = ResolvePagination(query.Page, query.PageSize, query.Skip, query.Take, 100, 1000);
    var rows = await reportsService.GetRuntimeEventsAsync(query.LineId, fromUtc, toUtc, skip, take, cancellationToken);
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

app.MapGet("/health/live", () =>
{
    return Results.Ok(new
    {
        status = "live",
        timestampUtc = DateTime.UtcNow,
    });
});

app.MapGet("/health/ready", async (PlantMonitorDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    if (!canConnect)
    {
        return ServiceUnavailableProblem(
            "Database connectivity check failed.",
            "database_unavailable");
    }

    return Results.Ok(new
    {
        status = "ready",
        timestampUtc = DateTime.UtcNow,
    });
});

app.MapHub<LinesHub>("/hubs/lines").RequireAuthorization();

app.MapFallback(async context =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    var indexPath = Path.Combine(webRoot, "index.html");
    if (!File.Exists(indexPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(indexPath);
});

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
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "RoutePath", "TEXT NOT NULL DEFAULT '1,0'");
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "ProcessorType", "TEXT NOT NULL DEFAULT 'ControlLogix'");
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "ConnectionTimeoutMs", "INTEGER NOT NULL DEFAULT 3000");
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "ReadTimeoutMs", "INTEGER NOT NULL DEFAULT 3000");
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "RetryCount", "INTEGER NOT NULL DEFAULT 1");
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "RetryDelayMs", "INTEGER NOT NULL DEFAULT 250");
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "IsActive", "INTEGER NOT NULL DEFAULT 1");
    EnsureSqliteColumn(dbContext, "line_protocol_assignments", "LineLifecycleState", "TEXT NOT NULL DEFAULT 'Draft'");

    dbContext.Database.ExecuteSqlRaw(@"
UPDATE line_protocol_assignments
SET LineNumber = CASE WHEN LineNumber = 0 THEN LineId ELSE LineNumber END,
    LineName = CASE WHEN trim(coalesce(LineName, '')) = '' THEN 'Line ' || LineId ELSE LineName END,
    RoutePath = CASE WHEN trim(coalesce(RoutePath, '')) = '' THEN '1,0' ELSE RoutePath END,
    ProcessorType = CASE WHEN trim(coalesce(ProcessorType, '')) = '' THEN 'ControlLogix' ELSE ProcessorType END,
    ConnectionTimeoutMs = CASE WHEN ConnectionTimeoutMs < 500 THEN 3000 ELSE ConnectionTimeoutMs END,
    ReadTimeoutMs = CASE WHEN ReadTimeoutMs < 500 THEN 3000 ELSE ReadTimeoutMs END,
    RetryCount = CASE WHEN RetryCount < 0 THEN 0 ELSE RetryCount END,
    RetryDelayMs = CASE WHEN RetryDelayMs < 0 THEN 0 ELSE RetryDelayMs END,
    LineLifecycleState = CASE
        WHEN trim(coalesce(LineLifecycleState, '')) = '' AND IsActive = 1 THEN 'Active'
        WHEN trim(coalesce(LineLifecycleState, '')) = '' AND IsActive = 0 THEN 'Disabled'
        ELSE LineLifecycleState
    END,
    IsActive = CASE
        WHEN LineLifecycleState = 'Active' THEN 1
        ELSE 0
    END;
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

    EnsureSqliteColumn(dbContext, "completed_production_runs", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(dbContext, "completed_production_runs", "DeletedByUsername", "TEXT NULL");
    EnsureSqliteColumn(dbContext, "completed_production_runs", "DeletedAtUtc", "TEXT NULL");

    if (!HasSqliteTable(dbContext, "local_users"))
    {
        dbContext.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS local_users (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,
    Role TEXT NOT NULL,
    IsDisabled INTEGER NOT NULL DEFAULT 0,
    MustChangePassword INTEGER NOT NULL DEFAULT 1,
    FailedLoginCount INTEGER NOT NULL DEFAULT 0,
    LockoutEndUtc TEXT NULL,
    LastLoginAtUtc TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_local_users_Username
    ON local_users (Username);
");
    }

    if (!HasSqliteTable(dbContext, "completed_run_deletion_audits"))
    {
        dbContext.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS completed_run_deletion_audits (
    Id TEXT NOT NULL PRIMARY KEY,
    RunId TEXT NOT NULL,
    LineId INTEGER NOT NULL,
    LineNumber INTEGER NOT NULL,
    LineName TEXT NOT NULL,
    ProductId TEXT NOT NULL,
    DeletedByUsername TEXT NOT NULL,
    DeletedByRole TEXT NOT NULL,
    DeletedAtUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_completed_run_deletion_audits_RunId
    ON completed_run_deletion_audits (RunId);
CREATE INDEX IF NOT EXISTS IX_completed_run_deletion_audits_DeletedAtUtc
    ON completed_run_deletion_audits (DeletedAtUtc);
");
    }
}

static void EnsureSqliteColumn(PlantMonitorDbContext dbContext, string tableName, string columnName, string columnDefinition)
{
    if (!HasSqliteTable(dbContext, tableName) || HasSqliteColumn(dbContext, tableName, columnName))
    {
        return;
    }

    if (!IsValidSqliteIdentifier(tableName) || !IsValidSqliteIdentifier(columnName))
    {
        throw new InvalidOperationException("Legacy schema upgrade attempted with an invalid SQLite identifier.");
    }

    if (!IsValidSqliteColumnDefinition(columnDefinition))
    {
        throw new InvalidOperationException("Legacy schema upgrade attempted with an invalid SQLite column definition.");
    }

    var sql = "ALTER TABLE \"" + tableName + "\" ADD COLUMN \"" + columnName + "\" " + columnDefinition + ";";
    dbContext.Database.ExecuteSqlRaw(sql);
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
        if (!IsValidSqliteIdentifier(tableName))
        {
            throw new InvalidOperationException("Legacy schema check attempted with an invalid SQLite identifier.");
        }

        command.CommandText = "PRAGMA table_info(\"" + tableName + "\");";
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

static bool IsValidSqliteIdentifier(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    foreach (var character in value)
    {
        if (!(char.IsLetterOrDigit(character) || character == '_'))
        {
            return false;
        }
    }

    return true;
}

static bool IsValidSqliteColumnDefinition(string value)
{
    if (string.IsNullOrWhiteSpace(value) || value.Contains(';', StringComparison.Ordinal))
    {
        return false;
    }

    foreach (var character in value)
    {
        var isAllowed =
            char.IsLetterOrDigit(character)
            || character == '_'
            || character == ' '
            || character == '('
            || character == ')'
            || character == ','
            || character == '.'
            || character == '\''
            || character == '+'
            || character == '-';

        if (!isAllowed)
        {
            return false;
        }
    }

    return true;
}

static string ResolveJwtSigningKey(IConfiguration configuration, IWebHostEnvironment environment, string configuredSigningKey)
{
    const string placeholder = "PlantMonitor_ChangeThisToAStrongSigningKey_2026";
    var key = configuredSigningKey?.Trim() ?? string.Empty;

    if (!string.IsNullOrWhiteSpace(key) && key != placeholder && key.Length >= 32)
    {
        return key;
    }

    var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    var secretsDir = Path.Combine(programData, "PlantMonitor", "Secrets");
    var secretFile = Path.Combine(secretsDir, "jwt.key");

    if (File.Exists(secretFile))
    {
        var stored = File.ReadAllText(secretFile).Trim();
        if (!string.IsNullOrWhiteSpace(stored) && stored.Length >= 32)
        {
            return stored;
        }
    }

    if (!environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "A strong JWT signing key is required in production. Set Jwt:SigningKey or provision ProgramData\\PlantMonitor\\Secrets\\jwt.key.");
    }

    Directory.CreateDirectory(secretsDir);
    var generated = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
    File.WriteAllText(secretFile, generated);
    return generated;
}

static string ResolvePlantMonitorConnectionString(string configuredConnectionString, IWebHostEnvironment environment)
{
    var sqlite = new SqliteConnectionStringBuilder(configuredConnectionString);
    if (string.IsNullOrWhiteSpace(sqlite.DataSource)
        || sqlite.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
        || Path.IsPathRooted(sqlite.DataSource)
        || environment.IsDevelopment())
    {
        return sqlite.ToString();
    }

    var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    var dataDir = Path.Combine(programData, "PlantMonitor", "Data");
    Directory.CreateDirectory(dataDir);

    sqlite.DataSource = Path.Combine(dataDir, sqlite.DataSource);
    return sqlite.ToString();
}

static IDisposable AcquireSingleInstanceLock(string connectionString)
{
    var sqlite = new SqliteConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(sqlite.DataSource)
        || sqlite.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
    {
        return NoopDisposable.Instance;
    }

    var databasePath = Path.GetFullPath(sqlite.DataSource);
    var lockDirectory = Path.Combine(Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory, ".locks");
    Directory.CreateDirectory(lockDirectory);

    var hashBytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(databasePath));
    var hashPrefix = Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    var lockFilePath = Path.Combine(lockDirectory, $"plantmonitor-{hashPrefix}.lck");

    try
    {
        var stream = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        stream.SetLength(0);
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write($"pid={Environment.ProcessId};db={databasePath};started={DateTime.UtcNow:O}");
        writer.Flush();
        stream.Position = 0;
        return stream;
    }
    catch (IOException exception)
    {
        throw new InvalidOperationException(
            $"Another PlantMonitor instance is already running for database '{databasePath}'.",
            exception);
    }
}

static (int Skip, int Take) ResolvePagination(
    int? page,
    int? pageSize,
    int? skip,
    int? take,
    int defaultTake,
    int maxTake)
{
    var normalizedTake = Math.Clamp(take ?? pageSize ?? defaultTake, 1, maxTake);
    if (page.HasValue && page.Value > 0)
    {
        return ((page.Value - 1) * normalizedTake, normalizedTake);
    }

    return (Math.Max(0, skip ?? 0), normalizedTake);
}

static IResult BadRequestProblem(string detail, string code)
{
    return Results.Problem(
        detail: detail,
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid request parameters.",
        type: "https://httpstatuses.com/400",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
        });
}

static IResult NotFoundProblem(string detail, string code)
{
    return Results.Problem(
        detail: detail,
        statusCode: StatusCodes.Status404NotFound,
        title: "Resource not found.",
        type: "https://httpstatuses.com/404",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
        });
}

static IResult LockedProblem(string detail, string code)
{
    return Results.Problem(
        detail: detail,
        statusCode: StatusCodes.Status423Locked,
        title: "Resource is locked.",
        type: "https://httpstatuses.com/423",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
        });
}

static IResult ServiceUnavailableProblem(string detail, string code)
{
    return Results.Problem(
        detail: detail,
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Service temporarily unavailable.",
        type: "https://httpstatuses.com/503",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
        });
}

public partial class Program;

// Query DTOs used by minimal APIs
public sealed class ProductionRunsQuery
{
    public int? LineId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public int? Skip { get; init; }
    public int? Take { get; init; } = 50;
}

public sealed class RecipeToleranceQuery
{
    public string? RecipeId { get; init; }
    public string? ProductId { get; init; }
}

public sealed class RuntimeEventsQuery
{
    public int? LineId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public int? Skip { get; init; }
    public int? Take { get; init; } = 100;
}

file sealed class NoopDisposable : IDisposable
{
    public static NoopDisposable Instance { get; } = new();

    private NoopDisposable()
    {
    }

    public void Dispose()
    {
    }
}