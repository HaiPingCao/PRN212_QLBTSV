using QLBT.Shared;

namespace QLBT.Server.Asp.SessionAndAuthentication
{
    public sealed class Session
    {
        public string Id { get; init; } = "";

        public int UserId { get; init; }

        public string Username { get; init; } = "";

        public UserRole Role { get; init; }

        public DateTime LastSeenUtc { get; set; }
    }
}
