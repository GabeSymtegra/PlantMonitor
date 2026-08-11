namespace backend.DTOs.System;

public sealed class AdminReauthRequestDto
{
    public string Password { get; init; } = string.Empty;
    public string Scope { get; init; } = "ota-apply";
}

public sealed class AdminReauthResponseDto
{
    public required string Scope { get; init; }
    public required string Token { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime VerifiedAtUtc { get; init; }
}

public sealed class OtaPrepareApplyRequestDto
{
    public string TargetVersion { get; init; } = string.Empty;
    public string ReauthToken { get; init; } = string.Empty;
}

public sealed class OtaPrepareApplyResponseDto
{
    public required string Status { get; init; }
    public required string TargetVersion { get; init; }
    public required string Message { get; init; }
    public DateTime PreparedAtUtc { get; init; }
}
