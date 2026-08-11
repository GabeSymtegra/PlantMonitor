namespace backend.Interfaces;

public interface IAdminReauthService
{
    string IssueToken(string username, string scope, TimeSpan lifetime);
    bool ValidateToken(string username, string scope, string token, bool consume);
}
