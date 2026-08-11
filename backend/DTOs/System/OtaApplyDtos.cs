namespace backend.DTOs.System;

public sealed class OtaApplyRequestDto
{
    public string StageOperationId { get; init; } = string.Empty;
    public string ReauthToken { get; init; } = string.Empty;
    public bool ForceHealthFailure { get; init; }
}

public sealed class OtaApplyOperationStatusDto
{
    public required string OperationId { get; init; }
    public required string Status { get; set; }
    public required string TargetVersion { get; init; }
    public required string PreviousVersion { get; init; }
    public required string CurrentVersion { get; set; }
    public string? AppliedPackagePath { get; set; }
    public string? HealthCheckStatus { get; set; }
    public bool RolledBack { get; set; }
    public string? Message { get; set; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; set; }
}
