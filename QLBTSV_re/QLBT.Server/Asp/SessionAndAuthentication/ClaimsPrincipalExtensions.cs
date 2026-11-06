using QLBT.Shared;
using System.Security.Claims;

namespace QLBT.Server.Asp.SessionAndAuthentication
{
    public static class ClaimsPrincipalExtensions
    {
        public static int GetUserId(this ClaimsPrincipal user)
        {
            return int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        public static UserRole GetRole(this ClaimsPrincipal user)
        {
            return Enum.Parse<UserRole>(user.FindFirstValue(ClaimTypes.Role)!);
        }

        public static string GetSessionId(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.Sid)!;
        }
    }
}
