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
builder.Services.AddAuthorization();

builder.Services.AddCors();
builder.Services.AddSignalR();
builder.Services.AddHostedService<LineUpdateBroadcastService>();

var app = builder.Build();

app.UseCors(policy =>
{
    policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/login", (LoginRequestDto request, IJwtTokenService jwtTokenService) =>
{
    var credentials = new Dictionary<string, (string Password, string Role)>(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] = ("admin", "Admin"),
        ["operator"] = ("operator", "Operator"),
        ["viewer"] = ("viewer", "Viewer"),
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

app.MapHub<LinesHub>("/hubs/lines");

app.Run();