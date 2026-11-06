using Tcp.Shared;

namespace Tcp.Server.Models;

public sealed class Session
{
    public string Id { get; }
    public int UserId { get; }
    public UserRole Role { get; }
    public DateTime LastSeenUtc { get; internal set; }

    internal Session(string id, int userId, UserRole role)
    {
        Id = id;
        UserId = userId;
        Role = role;
        LastSeenUtc = DateTime.UtcNow;
    }
}
