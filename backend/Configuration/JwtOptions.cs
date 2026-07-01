namespace backend.Configuration;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "PlantMonitor";
    public string Audience { get; set; } = "PlantMonitor.Frontend";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 120;
}
