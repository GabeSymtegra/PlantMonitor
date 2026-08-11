namespace backend.DTOs.System;

public sealed class OtaStagePackageRequestDto
{
    public string TargetVersion { get; init; } = string.Empty;
    public string PackageUrl { get; init; } = string.Empty;
    public string? ExpectedSha256 { get; init; }
    public string ReauthToken { get; init; } = string.Empty;
}

public sealed class OtaStageOperationStatusDto
{
    public required string OperationId { get; init; }
    public required string Status { get; set; }
    public required string TargetVersion { get; init; }
    public string? PackagePath { get; set; }
    public long? PackageSizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public bool? IsChecksumMatch { get; set; }
    public string? Message { get; set; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; set; }
}
