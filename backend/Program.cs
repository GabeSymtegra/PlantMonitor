using System.Security.Claims;
using System.Text;
using backend.Configuration;
using backend.Data;
using backend.DTOs.Authentication;
using backend.Interfaces;
using backend.Interfaces.Plc;
using backend.Services.Line;
using backend.Services.Authentication;
using backend.Services.Plc;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

var connectionString = builder.Configuration.GetConnectionString("PlantMonitor")
    ?? "Data Source=plantmonitor.db";

builder.Services.AddDbContext<PlantMonitorDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IPlcTagAddressValidator, PlcTagAddressValidator>();
builder.Services.AddScoped<IPlcProtocolConfigService, EfPlcProtocolConfigService>();
builder.Services.AddControllers();

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
        }
    }

    await PlcPresetSeeder.SeedAsync(dbContext);
}

app.UseCors("FrontendDev");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

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

public partial class Program;