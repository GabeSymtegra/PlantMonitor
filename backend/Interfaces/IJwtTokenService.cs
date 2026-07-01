namespace backend.Interfaces;

public interface IJwtTokenService
{
    string CreateToken(string username, string role);
}
