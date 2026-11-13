using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using QLBT.Server.Asp.SessionAndAuthentication;
using QLBT.Server.Models;
using QLBT.Shared;

namespace QLBT.Server.Asp.Endpoints;

public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost(ApiRoutes.Login, Login).AllowAnonymous();
        app.MapPost(ApiRoutes.Logout, Logout).RequireAuthorization();
        app.MapGet(ApiRoutes.Me, Me).RequireAuthorization();
    }

    private static async Task<IResult> Login(LoginRequest request, Prn212PQlbtContext db, SessionStore sessions)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return ApiResults.Fail(ApiStatus.InvalidCredentials, "Please enter your username and password.");
        }

        User? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return ApiResults.Fail(ApiStatus.InvalidCredentials, "The username or password is incorrect.");
        }
        if (!Enum.IsDefined(typeof(UserRole), user.RoleId))
        {
            return ApiResults.Fail(ApiStatus.Forbidden, "This account is not permitted to use the client application.");
        }

        UserRole role = (UserRole)user.RoleId;
        Session session = sessions.Create(user.Id, user.Username, role);
        return Results.Ok(new LoginResponse { SessionId = session.Id, User = ToDto(user) });
    }

    private static IResult Logout(ClaimsPrincipal user, SessionStore sessions)
    {
        sessions.Remove(user.GetSessionId());
        return Results.Ok();
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, Prn212PQlbtContext db)
    {
        int userId = principal.GetUserId();
        User? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "The user account no longer exists.");
        }
        return Results.Ok(ToDto(user));
    }

    private static UserDto ToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Role = (UserRole)user.RoleId
        };
    }
}
