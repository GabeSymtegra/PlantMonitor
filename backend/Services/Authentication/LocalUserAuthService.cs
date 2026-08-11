using backend.Data;
using backend.Interfaces.Authentication;
using backend.Models.Authentication;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Authentication;

public sealed class LocalUserAuthService : ILocalUserAuthService
{
    private const int LockoutThreshold = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly PlantMonitorDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<LocalUserEntity> _passwordHasher;

    public LocalUserAuthService(
        PlantMonitorDbContext dbContext,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IPasswordHasher<LocalUserEntity> passwordHasher)
    {
        _dbContext = dbContext;
        _environment = environment;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
    }

    public async Task EnsureBootstrapUsersAsync(CancellationToken cancellationToken = default)
    {
        var isDevelopmentLikeEnvironment = _environment.IsDevelopment() || string.Equals(_environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);

        var seedUsers = new List<(string Username, string Password, string Role, bool MustChangePassword)>();
        var existingUsers = await _dbContext.LocalUsers
            .AsNoTracking()
            .Select(x => new { x.Username, x.Role })
            .ToListAsync(cancellationToken);

        var existingAdmins = existingUsers.Any(x => x.Role == "Admin");
        var existingUsernames = new HashSet<string>(existingUsers.Select(x => x.Username), StringComparer.OrdinalIgnoreCase);

        if (isDevelopmentLikeEnvironment)
        {
            seedUsers.Add(("test", "test", "Admin", false));
            seedUsers.Add(("operator", "test", "Operator", false));
            seedUsers.Add(("viewer", "test", "Viewer", false));
        }
        else
        {
            if (!existingAdmins)
            {
                var bootstrapPassword = _configuration["Auth:BootstrapAdminPassword"];
                if (string.IsNullOrWhiteSpace(bootstrapPassword))
                {
                    throw new InvalidOperationException("Auth:BootstrapAdminPassword must be provided for first production startup.");
                }

                seedUsers.Add(("admin", bootstrapPassword, "Admin", true));
            }

            AddConfiguredBootstrapUser(seedUsers, existingUsernames, "Auth:BootstrapViewerUsername", "viewer", "Auth:BootstrapViewerPassword", "Viewer");
            AddConfiguredBootstrapUser(seedUsers, existingUsernames, "Auth:BootstrapOperatorUsername", "operator", "Auth:BootstrapOperatorPassword", "Operator");
        }

        if (isDevelopmentLikeEnvironment)
        {
            existingAdmins = true;
        }

        if (seedUsers.Count == 0)
        {
            return;
        }

        foreach (var seed in seedUsers)
        {
            if (existingUsernames.Contains(seed.Username))
            {
                continue;
            }

            var user = new LocalUserEntity
            {
                Username = seed.Username,
                Role = seed.Role,
                MustChangePassword = seed.MustChangePassword,
                IsDisabled = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, seed.Password);
            _dbContext.LocalUsers.Add(user);
        }

        if (!_dbContext.ChangeTracker.HasChanges())
        {
            return;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlite && sqlite.SqliteErrorCode == 19)
        {
            // Parallel startup races may attempt to seed the same bootstrap users.
            // If another host inserted first, uniqueness violations are safe to ignore.
        }
    }

    private void AddConfiguredBootstrapUser(
        ICollection<(string Username, string Password, string Role, bool MustChangePassword)> seedUsers,
        ISet<string> existingUsernames,
        string usernameConfigKey,
        string defaultUsername,
        string passwordConfigKey,
        string role)
    {
        var password = _configuration[passwordConfigKey]?.Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var username = _configuration[usernameConfigKey]?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            username = defaultUsername;
        }

        if (existingUsernames.Contains(username) || seedUsers.Any(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        seedUsers.Add((username, password, role, false));
    }

    public async Task<LocalAuthResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var user = await _dbContext.LocalUsers.SingleOrDefaultAsync(x => x.Username.ToLower() == normalized, cancellationToken);

        if (user is null)
        {
            return new LocalAuthResult(false, false, null, null, "Invalid username or password.");
        }

        if (user.IsDisabled)
        {
            return new LocalAuthResult(false, false, null, null, "Account is disabled.");
        }

        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            return new LocalAuthResult(false, true, user.LockoutEndUtc, null, "Account is temporarily locked.");
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verify == PasswordVerificationResult.Failed)
        {
            if (!_environment.IsDevelopment())
            {
                user.FailedLoginCount += 1;
                if (user.FailedLoginCount >= LockoutThreshold)
                {
                    user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
                    user.FailedLoginCount = 0;
                }

                user.UpdatedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return new LocalAuthResult(false, user.LockoutEndUtc.HasValue, user.LockoutEndUtc, null, "Invalid username or password.");
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = DateTime.UtcNow;

        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new LocalAuthResult(true, false, null, user, null);
    }

    public async Task<bool> ChangePasswordAsync(string username, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var user = await _dbContext.LocalUsers.SingleOrDefaultAsync(x => x.Username.ToLower() == normalized, cancellationToken);
        if (user is null || user.IsDisabled)
        {
            return false;
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (verify == PasswordVerificationResult.Failed)
        {
            return false;
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> VerifyPasswordAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var user = await _dbContext.LocalUsers.SingleOrDefaultAsync(x => x.Username.ToLower() == normalized, cancellationToken);

        if (user is null || user.IsDisabled)
        {
            return false;
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verify == PasswordVerificationResult.Failed)
        {
            return false;
        }

        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public Task<LocalUserEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        return _dbContext.LocalUsers.SingleOrDefaultAsync(x => x.Username.ToLower() == normalized, cancellationToken)!;
    }

}
