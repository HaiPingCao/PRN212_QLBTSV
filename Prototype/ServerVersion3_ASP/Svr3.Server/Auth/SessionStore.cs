using System.Collections.Concurrent;
using System.Security.Cryptography;
using Svr3.Shared;

namespace Svr3.Server.Auth;

public sealed class Session
{
    public string Id { get; init; } = "";

    public int UserId { get; init; }

    public string Username { get; init; } = "";

    public UserRole Role { get; init; }

    public DateTime LastSeenUtc { get; set; }
}

/// <summary>
/// In-memory sessions, same model as the TCP server: an opaque random id that Logout can revoke
/// (a JWT could not be revoked without a blacklist). Sessions are lost when the server restarts.
/// </summary>
public sealed class SessionStore
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new ConcurrentDictionary<string, Session>();
    private readonly TimeSpan _idleTimeout;

    public SessionStore(TimeSpan idleTimeout)
    {
        _idleTimeout = idleTimeout;
    }

    public Session Create(int userId, string username, UserRole role)
    {
        Session session = new Session
        {
            Id = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            UserId = userId,
            Username = username,
            Role = role,
            LastSeenUtc = DateTime.UtcNow
        };
        _sessions[session.Id] = session;
        return session;
    }

    /// <summary>Returns null for unknown or idle-expired ids. Touches the session (sliding expiry).</summary>
    public Session? Get(string id)
    {
        if (!_sessions.TryGetValue(id, out Session? session))
        {
            return null;
        }

        DateTime now = DateTime.UtcNow;
        if (now - session.LastSeenUtc > _idleTimeout)
        {
            _sessions.TryRemove(id, out _);
            return null;
        }

        session.LastSeenUtc = now;
        return session;
    }

    public void Remove(string id)
    {
        _sessions.TryRemove(id, out _);
    }
}
