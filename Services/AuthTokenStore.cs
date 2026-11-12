using System.Collections.Concurrent;

namespace BookStoreApi.Services;

public sealed class AuthSession
{
    public int UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateTime IssuedAtUtc { get; init; }
}

public class AuthTokenStore
{
    private readonly ConcurrentDictionary<string, AuthSession> _sessions = new();

    public string CreateToken(int userId, string userName, string role)
    {
        var token = Guid.NewGuid().ToString("N");
        _sessions[token] = new AuthSession
        {
            UserId = userId,
            UserName = userName,
            Role = role,
            IssuedAtUtc = DateTime.UtcNow
        };
        return token;
    }

    public bool TryGetSession(string? token, out AuthSession? session)
    {
        session = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return _sessions.TryGetValue(token, out session);
    }

    public bool IsRole(string? token, params string[] allowedRoles)
    {
        if (!TryGetSession(token, out var session) || session is null)
        {
            return false;
        }

        return allowedRoles.Any(r => string.Equals(r, session.Role, StringComparison.OrdinalIgnoreCase));
    }

    public AuthSession? GetSession(string? token)
    {
        return TryGetSession(token, out var session) ? session : null;
    }
}
