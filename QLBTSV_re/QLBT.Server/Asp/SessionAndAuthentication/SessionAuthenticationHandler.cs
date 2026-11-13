using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QLBT.Shared;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace QLBT.Server.Asp.SessionAndAuthentication;

/// <summary>
/// ASP.NET authentication scheme over <see cref="SessionStore"/>. Reads "Authorization: Bearer {sessionId}"
/// and turns the session into a ClaimsPrincipal, so endpoints can use RequireAuthorization / roles.
/// Role and user id come only from the server-side session, never from the request.
/// </summary>
public sealed class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Session";

    private readonly SessionStore _sessions;

    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        SessionStore sessions)
        : base(options, logger, encoder)
    {
        _sessions = sessions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string sessionId = header.Substring("Bearer ".Length).Trim();
        Session? session = _sessions.Get(sessionId);
        if (session == null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown or expired session."));
        }

        Claim[] claims =
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim(ClaimTypes.Name, session.Username),
            new Claim(ClaimTypes.Role, session.Role.ToString()),
            new Claim(ClaimTypes.Sid, session.Id)
        };
        ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    /// <summary>401: not logged in or session gone. Client clears its SessionId.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        return WriteError(StatusCodes.Status401Unauthorized, ApiStatus.Unauthorized, "You are not logged in, or your session has expired.");
    }

    /// <summary>403: logged in but wrong role.</summary>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        return WriteError(StatusCodes.Status403Forbidden, ApiStatus.Forbidden, "Your role does not permit this operation.");
    }

    private Task WriteError(int statusCode, ApiStatus status, string error)
    {
        Response.StatusCode = statusCode;
        return Response.WriteAsJsonAsync(new ApiError { Status = status, Error = error }, ApiJson.Options);
    }
}