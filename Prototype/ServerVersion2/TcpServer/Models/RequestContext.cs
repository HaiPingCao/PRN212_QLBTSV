using Tcp.Server;
using Tcp.Server.Models;
using Tcp.Shared;

namespace Server.Models;

/// <summary>Everything a handler needs for one request. Valid only during IRequestHandler.Handle.</summary>
public sealed class RequestContext
{
    private readonly Stream stream;
    private Session? session;

    public Request Request { get; }
    public ServerConfig Config { get; }
    public SessionsManager Sessions { get; }
    public FileStorage Storage { get; }

    public bool ResponseSent { get; private set; }

    /// <summary>Status of the response sent, for the server log. Null until sent.</summary>
    internal ResponseStatus? SentStatus { get; private set; }

    /// <summary>True once the uploaded file bytes have been read from the connection.</summary>
    internal bool FileConsumed { get; private set; }

    /// <summary>Session set by a successful RequireRole, for the server log. Null otherwise.</summary>
    internal Session? LoadedSession
    {
        get { return session; }
    }

    internal RequestContext(Request request, Stream stream, ServerConfig config, SessionsManager sessions)
    {
        Request = request;
        this.stream = stream;
        Config = config;
        Sessions = sessions;
        Storage = new FileStorage(config.StoragePath);
    }

    /// <summary>The caller's session.</summary>
    /// <exception cref="InvalidOperationException">RequireRole wasn't called, or returned false.</exception>
    public Session Session
    {
        get
        {
            if (session == null)
            {
                throw new InvalidOperationException("No session. Call RequireRole first.");
            }
            return session;
        }
    }

    /// <summary>
    /// Checks the request's SessionId and the caller's role. On failure, sends Unauthorized (no valid session)
    /// or Forbidden (role not allowed) and returns false; the handler must then return without responding.
    /// On success, Session becomes available.
    /// </summary>
    public async Task<bool> RequireRole(params UserRole[] allowedRoles)
    {
        if (allowedRoles.Length == 0)
        {
            throw new ArgumentException("At least one role is required.", nameof(allowedRoles));
        }

        Session? found = Sessions.Get(Request.SessionId);
        if (found == null)
        {
            await Fail(ResponseStatus.Unauthorized, "Session expired or invalid. Please log in again.");
            return false;
        }
        if (Array.IndexOf(allowedRoles, found.Role) < 0)
        {
            await Fail(ResponseStatus.Forbidden, "Your role can't perform " + Request.Type + ".");
            return false;
        }

        session = found;
        return true;
    }

    public Task Ok()
    {
        return Send(Response.Ok(Request.Type));
    }

    public Task Ok<TBody>(TBody body)
    {
        return Send(Response.Ok(Request.Type, body));
    }

    public Task Fail(ResponseStatus status, string error)
    {
        return Send(Response.Fail(Request.Type, status, error));
    }

    /// <summary>For responses built by hand, e.g. Login setting SessionId.</summary>
    /// <exception cref="InvalidOperationException">Already sent, wrong Type, or FileLength set (use SendFile).</exception>
    public async Task Send(Response response)
    {
        if (ResponseSent)
        {
            throw new InvalidOperationException("A response was already sent.");
        }
        if (response.Type != Request.Type)
        {
            throw new InvalidOperationException("Response type " + response.Type + " doesn't match request type " + Request.Type + ".");
        }
        if (response.FileLength != null)
        {
            throw new InvalidOperationException("Use SendFile to send a file.");
        }

        ResponseSent = true;
        SentStatus = response.Status;
        await Wire.WriteResponse(stream, response);
    }

    /// <summary>
    /// Saves the uploaded file to <paramref name="finalPath"/>. Bytes go to "{finalPath}.tmp" first and replace
    /// the final file only after all of them arrive, so a dropped connection keeps the previous file.
    /// Call before sending the response.
    /// </summary>
    /// <exception cref="InvalidDataException">Request carries no file.</exception>
    /// <exception cref="EndOfStreamException">Client disconnected mid-upload; the previous file is untouched.</exception>
    public async Task ReceiveFile(string finalPath)
    {
        if (Request.FileLength == null)
        {
            throw new InvalidDataException("Request has no file.");
        }
        if (FileConsumed)
        {
            throw new InvalidOperationException("File was already received.");
        }
        FileConsumed = true;

        string? directory = Path.GetDirectoryName(finalPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = finalPath + ".tmp";
        try
        {
            await using (FileStream file = File.Create(tempPath))
            {
                await Wire.ReadFile(stream, file, Request.FileLength.Value);
            }
            File.Move(tempPath, finalPath, true);
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
    }

    /// <summary>Sends an Ok response with FileLength set, then the file bytes.</summary>
    /// <exception cref="FileNotFoundException">File missing on disk; nothing was sent.</exception>
    public Task SendFile(string path)
    {
        return SendFileCore(path, Response.Ok(Request.Type));
    }

    /// <summary>Same as SendFile(path), with a body (e.g. the original file name).</summary>
    public Task SendFile<TBody>(string path, TBody body)
    {
        return SendFileCore(path, Response.Ok(Request.Type, body));
    }

    private async Task SendFileCore(string path, Response response)
    {
        if (ResponseSent)
        {
            throw new InvalidOperationException("A response was already sent.");
        }

        await using (FileStream file = File.OpenRead(path))
        {
            response.FileLength = file.Length;
            ResponseSent = true;
            SentStatus = response.Status;
            await Wire.WriteResponse(stream, response);
            await Wire.WriteFile(stream, file, file.Length);
        }
    }
}
