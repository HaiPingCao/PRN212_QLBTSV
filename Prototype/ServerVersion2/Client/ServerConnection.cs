using System.Net.Sockets;
using Tcp.Shared;

namespace Tcp.Client;

/// <summary>
/// Client side of the protocol. Each call opens one TCP connection, sends one request and reads one response.
/// Keeps the SessionId: set by a successful Login, cleared by Logout or any Unauthorized response.
///
/// Network failures throw (SocketException, IOException, TimeoutException).
/// Server-side failures do not throw: check Response.Status.
/// </summary>
public sealed class ServerConnection
{
    public string Host { get; set; }
    public int Port { get; set; }

    /// <summary>Applies to connecting only. Transfers have no time limit.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public string? SessionId { get; private set; }

    public bool IsLoggedIn
    {
        get { return SessionId != null; }
    }

    /// <summary>Raised when the server answers Unauthorized. The UI should return to the login screen.</summary>
    public event EventHandler? SessionExpired;

    public ServerConnection(string host, int port)
    {
        Host = host;
        Port = port;
    }

    /// <summary>On Ok, stores the SessionId from the response.</summary>
    public async Task<Response> Login<TBody>(TBody body)
    {
        Response response = await Exchange(Request.Create(MessageType.Login, null, body), null, null);
        if (response.IsOk)
        {
            SessionId = response.SessionId;
        }
        return response;
    }

    /// <summary>Always clears the local session, even if the server is unreachable.</summary>
    public async Task Logout()
    {
        string? sessionId = SessionId;
        SessionId = null;
        if (sessionId == null)
        {
            return;
        }

        try
        {
            await Exchange(Request.Create(MessageType.Logout, sessionId), null, null);
        }
        catch (Exception ex) when (ex is SocketException || ex is IOException || ex is TimeoutException)
        {
            // Server unreachable: the session will expire there on its own.
        }
    }

    public Task<Response> Send(MessageType type)
    {
        return Exchange(Request.Create(type, SessionId), null, null);
    }

    public Task<Response> Send<TBody>(MessageType type, TBody body)
    {
        return Exchange(Request.Create(type, SessionId, body), null, null);
    }

    /// <summary>Sends the request followed by the file's bytes.</summary>
    public Task<Response> Upload<TBody>(MessageType type, TBody body, string filePath)
    {
        return Exchange(Request.Create(type, SessionId, body), filePath, null);
    }

    /// <summary>
    /// If the response is Ok and carries a file, saves it to <paramref name="savePath"/>
    /// (via "{savePath}.tmp", so an interrupted download never leaves a half-written file).
    /// </summary>
    public Task<Response> Download<TBody>(MessageType type, TBody body, string savePath)
    {
        return Exchange(Request.Create(type, SessionId, body), null, savePath);
    }

    private async Task<Response> Exchange(Request request, string? uploadPath, string? savePath)
    {
        using (TcpClient client = new TcpClient())
        {
            await client.ConnectAsync(Host, Port).WaitAsync(ConnectTimeout);
            NetworkStream stream = client.GetStream();

            if (uploadPath == null)
            {
                await Wire.WriteRequest(stream, request);
            }
            else
            {
                await using (FileStream file = File.OpenRead(uploadPath))
                {
                    request.FileLength = file.Length;
                    await Wire.WriteRequest(stream, request);
                    await Wire.WriteFile(stream, file, file.Length);
                }
            }

            Response response = await Wire.ReadResponse(stream);

            if (response.FileLength != null)
            {
                if (savePath == null)
                {
                    throw new InvalidDataException("Server sent a file for " + request.Type + ", which expects none.");
                }
                await ReceiveFile(stream, savePath, response.FileLength.Value);
            }

            if (response.Status == ResponseStatus.Unauthorized)
            {
                SessionId = null;
                EventHandler? handler = SessionExpired;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            return response;
        }
    }

    private static async Task ReceiveFile(Stream stream, string savePath, long length)
    {
        string tempPath = savePath + ".tmp";
        try
        {
            await using (FileStream file = File.Create(tempPath))
            {
                await Wire.ReadFile(stream, file, length);
            }
            File.Move(tempPath, savePath, true);
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
    }
}
