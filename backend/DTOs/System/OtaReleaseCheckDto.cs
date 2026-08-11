namespace backend.DTOs.System;

public sealed class OtaReleaseCheckDto
{
    public required string Status { get; init; }
    public required string CurrentVersion { get; init; }
    public string? LatestVersion { get; init; }
    public bool HasUpdate { get; init; }
    public string? ReleaseUrl { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public string? Summary { get; init; }
    public required string Message { get; init; }
    public DateTime CheckedAtUtc { get; init; }
}
