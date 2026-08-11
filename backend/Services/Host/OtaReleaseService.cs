using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using backend.DTOs.System;
using backend.Interfaces;

namespace backend.Services.Host;

public sealed class OtaReleaseService : IOtaReleaseService
{
    private const string GitHubApiBase = "https://api.github.com";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public OtaReleaseService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<OtaReleaseCheckDto> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = ResolveCurrentVersion();
        var repo = ResolveRepository();

        try
        {
            var client = _httpClientFactory.CreateClient("GitHubReleases");
            var response = await client.GetAsync($"/repos/{repo}/releases/latest", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                response = await client.GetAsync($"/repos/{repo}/releases?per_page=1", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var fallbackList = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<GitHubReleaseDto>>(cancellationToken: cancellationToken);
                    var fallbackRelease = fallbackList?.FirstOrDefault();
                    if (fallbackRelease is not null && !string.IsNullOrWhiteSpace(fallbackRelease.TagName))
                    {
                        var fallbackLatestVersion = NormalizeTag(fallbackRelease.TagName);
                        var fallbackHasUpdate = IsNewerRelease(currentVersion, fallbackLatestVersion);

                        return new OtaReleaseCheckDto
                        {
                            Status = "ok",
                            CurrentVersion = currentVersion,
                            LatestVersion = fallbackLatestVersion,
                            HasUpdate = fallbackHasUpdate,
                            ReleaseUrl = fallbackRelease.HtmlUrl,
                            PublishedAtUtc = fallbackRelease.PublishedAtUtc,
                            Summary = Summarize(fallbackRelease.Body),
                            Message = fallbackHasUpdate
                                ? "A newer release is available."
                                : "You are running the latest available release.",
                            CheckedAtUtc = DateTime.UtcNow,
                        };
                    }
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                return new OtaReleaseCheckDto
                {
                    Status = "unavailable",
                    CurrentVersion = currentVersion,
                    LatestVersion = null,
                    HasUpdate = false,
                    ReleaseUrl = null,
                    PublishedAtUtc = null,
                    Summary = null,
                    Message = $"GitHub release check returned HTTP {(int)response.StatusCode}.",
                    CheckedAtUtc = DateTime.UtcNow,
                };
            }

            var payload = await response.Content.ReadFromJsonAsync<GitHubReleaseDto>(cancellationToken: cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.TagName))
            {
                return new OtaReleaseCheckDto
                {
                    Status = "unavailable",
                    CurrentVersion = currentVersion,
                    LatestVersion = null,
                    HasUpdate = false,
                    ReleaseUrl = null,
                    PublishedAtUtc = null,
                    Summary = null,
                    Message = "Latest release payload was empty.",
                    CheckedAtUtc = DateTime.UtcNow,
                };
            }

            var latestVersion = NormalizeTag(payload.TagName);
            var hasUpdate = IsNewerRelease(currentVersion, latestVersion);

            return new OtaReleaseCheckDto
            {
                Status = "ok",
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                HasUpdate = hasUpdate,
                ReleaseUrl = payload.HtmlUrl,
                PublishedAtUtc = payload.PublishedAtUtc,
                Summary = Summarize(payload.Body),
                Message = hasUpdate
                    ? "A newer release is available."
                    : "You are running the latest available release.",
                CheckedAtUtc = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            return new OtaReleaseCheckDto
            {
                Status = "error",
                CurrentVersion = currentVersion,
                LatestVersion = null,
                HasUpdate = false,
                ReleaseUrl = null,
                PublishedAtUtc = null,
                Summary = null,
                Message = $"Release check failed: {ex.Message}",
                CheckedAtUtc = DateTime.UtcNow,
            };
        }
    }

    private string ResolveRepository()
    {
        var configured = _configuration["App:OtaRepo"]?.Trim();
        return string.IsNullOrWhiteSpace(configured) ? "GabeSymtegra/PlantMonitor" : configured;
    }

    private static string ResolveCurrentVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var informational = entryAssembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return NormalizeTag(informational);
        }

        var assemblyVersion = entryAssembly?.GetName().Version?.ToString();
        if (!string.IsNullOrWhiteSpace(assemblyVersion))
        {
            return assemblyVersion;
        }

        return "unknown";
    }

    private static bool IsNewerRelease(string current, string latest)
    {
        if (!TryParseVersion(current, out var currentVersion)
            || !TryParseVersion(latest, out var latestVersion)
            || currentVersion is null
            || latestVersion is null)
        {
            return !string.Equals(current, latest, StringComparison.OrdinalIgnoreCase);
        }

        return latestVersion > currentVersion;
    }

    private static bool TryParseVersion(string value, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeTag(value);
        var tokens = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var numericParts = new List<int>();
        foreach (var token in tokens)
        {
            var digits = new string(token.TakeWhile(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits) || !int.TryParse(digits, out var parsed))
            {
                break;
            }

            numericParts.Add(parsed);
            if (numericParts.Count == 4)
            {
                break;
            }
        }

        if (numericParts.Count == 0)
        {
            return false;
        }

        while (numericParts.Count < 2)
        {
            numericParts.Add(0);
        }

        var versionText = string.Join('.', numericParts);
        if (!Version.TryParse(versionText, out var parsedVersion))
        {
            return false;
        }

        version = parsedVersion;
        return true;
    }

    private static string NormalizeTag(string value)
    {
        var withoutMetadata = value.Split('+', 2, StringSplitOptions.TrimEntries)[0];
        var trimmed = withoutMetadata.Trim();

        return trimmed.StartsWith('v') || trimmed.StartsWith('V')
            ? trimmed[1..]
            : trimmed;
    }

    private static string? Summarize(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var line = body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        return line.Length > 160 ? line[..160] + "..." : line;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAtUtc { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }
    }
}
