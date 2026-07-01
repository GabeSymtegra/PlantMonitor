var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();

var app = builder.Build();

app.UseCors(policy =>
{
    policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod();
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
});

app.Run();