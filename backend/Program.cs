using System.Security.Claims;
using System.Text;
using backend.Configuration;
using backend.DTOs.Authentication;
using backend.Interfaces;
using backend.Services.Line;
using backend.Services.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

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

app.UseCors("FrontendDev");

app.UseAuthentication();
app.UseAuthorization();

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