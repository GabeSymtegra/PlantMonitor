using backend.Models.Authentication;

namespace backend.Interfaces.Authentication;

public interface ILocalUserAuthService
{
    Task<LocalAuthResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<bool> VerifyPasswordAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<bool> ChangePasswordAsync(string username, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<LocalUserEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task EnsureBootstrapUsersAsync(CancellationToken cancellationToken = default);
}

public sealed record LocalAuthResult(
    bool Success,
    bool IsLockedOut,
    DateTime? LockoutEndUtc,
    LocalUserEntity? User,
    string? ErrorMessage);
