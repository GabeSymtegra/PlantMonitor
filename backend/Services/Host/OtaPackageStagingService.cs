using System.Collections.Concurrent;
using System.Security.Cryptography;
using backend.DTOs.System;
using backend.Interfaces;

namespace backend.Services.Host;

public sealed class OtaPackageStagingService : IOtaPackageStagingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, OtaStageOperationStatusDto> _operations =
        new(StringComparer.OrdinalIgnoreCase);

    public OtaPackageStagingService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<OtaStageOperationStatusDto> StagePackageAsync(
        OtaStagePackageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var operation = new OtaStageOperationStatusDto
        {
            OperationId = operationId,
            Status = "in_progress",
            TargetVersion = request.TargetVersion.Trim(),
            StartedAtUtc = DateTime.UtcNow,
            Message = "Package staging started.",
        };

        _operations[operationId] = operation;

        try
        {
            var stagingRoot = ResolveStagingRoot();
            var versionSegment = SanitizeSegment(operation.TargetVersion);
            var versionDirectory = Path.Combine(stagingRoot, versionSegment);
            Directory.CreateDirectory(versionDirectory);

            var sourceFileName = ResolveSourceFileName(request.PackageUrl);
            var destinationPath = Path.Combine(
                versionDirectory,
                $"{DateTime.UtcNow:yyyyMMddHHmmss}-{sourceFileName}");

            await using var sourceStream = await OpenSourceStreamAsync(request.PackageUrl, cancellationToken);
            await using var destinationStream = File.Create(destinationPath);

            var (sha256, sizeBytes) = await CopyWithSha256Async(sourceStream, destinationStream, cancellationToken);

            operation.PackagePath = destinationPath;
            operation.PackageSizeBytes = sizeBytes;
            operation.Sha256 = sha256;

            if (!string.IsNullOrWhiteSpace(request.ExpectedSha256))
            {
                var normalizedExpected = NormalizeSha256(request.ExpectedSha256);
                operation.IsChecksumMatch = string.Equals(sha256, normalizedExpected, StringComparison.OrdinalIgnoreCase);

                if (operation.IsChecksumMatch != true)
                {
                    operation.Status = "failed";
                    operation.Message = "SHA-256 verification failed for staged package.";
                    operation.CompletedAtUtc = DateTime.UtcNow;
                    return operation;
                }
            }
            else
            {
                operation.IsChecksumMatch = null;
            }

            operation.Status = "staged";
            operation.Message = "Package downloaded and staged successfully.";
            operation.CompletedAtUtc = DateTime.UtcNow;

            return operation;
        }
        catch (Exception ex)
        {
            operation.Status = "failed";
            operation.Message = $"Package staging failed: {ex.Message}";
            operation.CompletedAtUtc = DateTime.UtcNow;
            return operation;
        }
    }

    public OtaStageOperationStatusDto? GetStatus(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return null;
        }

        return _operations.TryGetValue(operationId.Trim(), out var value)
            ? value
            : null;
    }

    private string ResolveStagingRoot()
    {
        var configured = _configuration["App:OtaStagingRoot"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PlantMonitor", "OtaStaging");
    }

    private async Task<Stream> OpenSourceStreamAsync(string packageUrl, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(packageUrl, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme is "http" or "https")
            {
                var client = _httpClientFactory.CreateClient("GitHubReleases");
                var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStreamAsync(cancellationToken);
            }

            if (uri.Scheme == "file")
            {
                return File.OpenRead(uri.LocalPath);
            }

            throw new InvalidOperationException($"Unsupported package URL scheme '{uri.Scheme}'.");
        }

        return File.OpenRead(packageUrl);
    }

    private static async Task<(string sha256, long sizeBytes)> CopyWithSha256Async(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        var buffer = new byte[1024 * 64];
        long totalBytes = 0;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead <= 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            sha.TransformBlock(buffer, 0, bytesRead, null, 0);
            totalBytes += bytesRead;
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hash = Convert.ToHexString(sha.Hash ?? Array.Empty<byte>()).ToLowerInvariant();

        await destination.FlushAsync(cancellationToken);

        return (hash, totalBytes);
    }

    private static string ResolveSourceFileName(string packageUrl)
    {
        if (Uri.TryCreate(packageUrl, UriKind.Absolute, out var uri))
        {
            var fromUri = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fromUri))
            {
                return fromUri;
            }
        }

        var fromPath = Path.GetFileName(packageUrl);
        return string.IsNullOrWhiteSpace(fromPath) ? "ota-package.bin" : fromPath;
    }

    private static string NormalizeSha256(string value)
    {
        return value.Trim().Replace("-", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
    }

    private static string SanitizeSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown-version" : sanitized;
    }
}
