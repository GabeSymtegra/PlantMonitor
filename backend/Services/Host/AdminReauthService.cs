using System.Collections.Concurrent;
using backend.Interfaces;

namespace backend.Services.Host;

public sealed class AdminReauthService : IAdminReauthService
{
    private readonly ConcurrentDictionary<string, ReauthTokenRecord> _tokens =
        new(StringComparer.Ordinal);

    public string IssueToken(string username, string scope, TimeSpan lifetime)
    {
        PruneExpiredTokens();

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        _tokens[token] = new ReauthTokenRecord
        {
            Username = username,
            Scope = scope,
            ExpiresAtUtc = DateTime.UtcNow.Add(lifetime),
        };

        return token;
    }

    public bool ValidateToken(string username, string scope, string token, bool consume)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!_tokens.TryGetValue(token, out var record))
        {
            return false;
        }

        if (record.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _tokens.TryRemove(token, out _);
            return false;
        }

        var valid = string.Equals(record.Username, username, StringComparison.OrdinalIgnoreCase)
            && string.Equals(record.Scope, scope, StringComparison.OrdinalIgnoreCase);

        if (!valid)
        {
            return false;
        }

        if (consume)
        {
            _tokens.TryRemove(token, out _);
        }

        return true;
    }

    private void PruneExpiredTokens()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in _tokens)
        {
            if (entry.Value.ExpiresAtUtc <= now)
            {
                _tokens.TryRemove(entry.Key, out _);
            }
        }
    }

    private sealed class ReauthTokenRecord
    {
        public required string Username { get; init; }
        public required string Scope { get; init; }
        public required DateTime ExpiresAtUtc { get; init; }
    }
}
