namespace Tcp.Shared;

public enum ResponseStatus
{
    /// <summary>Success. Body holds the result, if the operation has one.</summary>
    Ok,

    /// <summary>Login only: wrong username or password.</summary>
    InvalidCredentials,

    /// <summary>Session missing, expired or invalid. Client clears its SessionId and returns to login.</summary>
    Unauthorized,

    /// <summary>Logged in, but not allowed: wrong role, not the owner, or suspended in the class.</summary>
    Forbidden,

    /// <summary>The requested record or file does not exist.</summary>
    NotFound,

    /// <summary>Allowed, but breaks a rule: assignment closed, submission already graded, semester started.</summary>
    Rejected,

    /// <summary>Malformed request or server failure.</summary>
    Error
}
