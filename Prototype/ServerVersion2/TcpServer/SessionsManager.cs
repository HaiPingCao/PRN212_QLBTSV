using System.Security.Cryptography;
using Tcp.Server.Models;
using Tcp.Shared;

namespace Tcp.Server;

/// <summary>In-memory sessions. All sessions are lost when the server process exits.</summary>
public sealed class SessionsManager
{
    private readonly Dictionary<string, Session> sessions = new Dictionary<string, Session>();
    private readonly object sync = new object();
    private TimeSpan timeout = TimeSpan.FromMinutes(120);

    /// <summary>Idle time after which a session expires. Set by TcpServer.Start from the config.</summary>
    public TimeSpan Timeout
    {
        get { lock (sync) { return timeout; } }
        set { lock (sync) { timeout = value; } }
    }

    /// <summary>Called by the Login handler after the password is verified.</summary>
    public Session Create(int userId, UserRole role)
    {
        // 32 random bytes -> 64 hex chars; not guessable.
        string id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Session session = new Session(id, userId, role);

        lock (sync)
        {
            RemoveExpired();
            sessions.Add(id, session);
        }
        return session;
    }

    /// <summary>Returns the session and refreshes its idle timer, or null if missing or expired.</summary>
    public Session? Get(string? id)
    {
        if (id == null)
        {
            return null;
        }

        lock (sync)
        {
            Session? session;
            if (!sessions.TryGetValue(id, out session))
            {
                return null;
            }
            if (IsExpired(session))
            {
                sessions.Remove(id);
                return null;
            }
            session.LastSeenUtc = DateTime.UtcNow;
            return session;
        }
    }

    public void Remove(string id)
    {
        lock (sync)
        {
            sessions.Remove(id);
        }
    }

    private bool IsExpired(Session session)
    {
        return DateTime.UtcNow - session.LastSeenUtc > timeout;
    }

    // Caller holds the lock.
    private void RemoveExpired()
    {
        List<string> expired = new List<string>();
        foreach (Session session in sessions.Values)
        {
            if (IsExpired(session))
            {
                expired.Add(session.Id);
            }
        }
        foreach (string id in expired)
        {
            sessions.Remove(id);
        }
    }
}
