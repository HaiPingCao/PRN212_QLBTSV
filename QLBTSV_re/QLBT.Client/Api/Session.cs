using QLBT.Shared;

namespace QLBT.Client.Api;

/// <summary>The one HTTP client of the app and the logged-in user (null before login / after logout).</summary>
public static class Session
{
    public static ApiClient Client { get; } = new ApiClient(new Uri(ClientSettings.Load().ServerUrl));

    public static UserDto? User { get; private set; }

    public static string UserDisplay
    {
        get { return User == null ? "" : User.Username + " · " + User.FullName + " (" + User.Role + ")"; }
    }

    public static async Task<ApiResult<LoginResponse>> Login(string username, string password)
    {
        ApiResult<LoginResponse> result = await Client.Login(username, password);
        User = result.IsOk ? result.Body?.User : null;
        return result;
    }

    /// <summary>Logs out on the server if it answers; the local session is cleared either way.</summary>
    public static async Task Logout()
    {
        User = null;
        await Client.Logout();
    }

    /// <summary>The server forgot the session (restart / Stop): drop it locally too.</summary>
    public static void Expire()
    {
        User = null;
    }
}
