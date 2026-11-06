namespace Svr3.Shared;

/// <summary>
/// Same statuses as the TCP protocol (Plan.md). Over HTTP each one also maps to a status code:
/// Ok 200, InvalidCredentials 401, Unauthorized 401, Forbidden 403, NotFound 404, Rejected 409, Error 500.
/// </summary>
public enum ApiStatus
{
    Ok,
    InvalidCredentials,
    Unauthorized,
    Forbidden,
    NotFound,
    Rejected,
    Error
}

/// <summary>Values match roles.id in the database.</summary>
public enum UserRole
{
    Teacher = 1,
    Student = 2
}

/// <summary>Body of every non-2xx response.</summary>
public sealed class ApiError
{
    public ApiStatus Status { get; set; }

    public string Error { get; set; } = "";
}
