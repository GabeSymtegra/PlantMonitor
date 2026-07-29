namespace backend.DTOs.Authentication;

public sealed class LoginResponseDto
{
    public string? AccessToken { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public bool MustChangePassword { get; set; }
}
