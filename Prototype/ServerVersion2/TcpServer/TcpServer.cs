using Server.Models;
using System.Net;
using System.Net.Sockets;
using Tcp.Server.Models;
using Tcp.Shared;

namespace Tcp.Server;

/// <summary>
/// Accepts TCP connections and passes each request to one IRequestHandler. One request per connection.
/// Usage:  new TcpServer(handler) -> Start(config) -> Stop(). Start again with a new config to apply changes.
/// </summary>
public sealed class TcpServer
{
    private readonly IRequestHandler handler;
    private TcpListener? listener;
    private CancellationTokenSource? stopSource;
    private Task? acceptLoop;

    /// <summary>Kept across Stop/Start, so clients stay logged in after a restart.</summary>
    public SessionsManager Sessions { get; } = new SessionsManager();

    public bool IsRunning
    {
        get { return listener != null; }
    }

    /// <summary>Raised from background threads. In WPF, use Dispatcher.BeginInvoke to touch the UI.</summary>
    public event EventHandler<string>? LogMessage;

    public TcpServer(IRequestHandler handler)
    {
        this.handler = handler;
    }

    /// <exception cref="InvalidDataException">Config is invalid.</exception>
    /// <exception cref="SocketException">Port is in use or not allowed.</exception>
    /// <exception cref="IOException">Storage folder can't be created.</exception>
    public void Start(ServerConfig config)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Server is already running.");
        }

        config.Validate();
        // Snapshot, so edits in the config screen don't affect the running server.
        ServerConfig snapshot = config.Clone();

        Directory.CreateDirectory(snapshot.StoragePath);
        Sessions.Timeout = TimeSpan.FromMinutes(snapshot.SessionTimeoutMinutes);

        TcpListener newListener = new TcpListener(IPAddress.Any, snapshot.Port);
        newListener.Start();

        listener = newListener;
        stopSource = new CancellationTokenSource();
        acceptLoop = AcceptLoop(newListener, snapshot, stopSource.Token);
        Log("Listening on port " + snapshot.Port + ", storage: " + snapshot.StoragePath);
    }

    /// <summary>Stops accepting connections. Requests already in progress finish on their own.</summary>
    public async Task Stop()
    {
        if (listener == null || stopSource == null || acceptLoop == null)
        {
            return;
        }

        stopSource.Cancel();
        listener.Stop();
        await acceptLoop;

        stopSource.Dispose();
        listener = null;
        stopSource = null;
        acceptLoop = null;
        Log("Stopped.");
    }

    private async Task AcceptLoop(TcpListener activeListener, ServerConfig config, CancellationToken stopToken)
    {
        while (!stopToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await activeListener.AcceptTcpClientAsync(stopToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                if (stopToken.IsCancellationRequested)
                {
                    break;
                }
                Log("Accept failed: " + ex.Message);
                continue;
            }

            // Not awaited: each connection runs on its own. HandleConnection never throws.
            _ = HandleConnection(client, config);
        }
    }

    private async Task HandleConnection(TcpClient client, ServerConfig config)
    {
        string remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        using (client)
        {
            try
            {
                NetworkStream stream = client.GetStream();

                Request request;
                try
                {
                    request = await Wire.ReadRequest(stream).WaitAsync(TimeSpan.FromSeconds(config.RequestTimeoutSeconds));
                }
                catch (Exception ex)
                {
                    // Timeout, malformed frame or disconnect. Request type is unknown, so no response is possible.
                    Log(remote + ": unreadable request (" + ex.GetType().Name + ": " + ex.Message + ")");
                    return;
                }

                RequestContext context = await Dispatch(request, stream, config, remote);

                // Upload not read by the handler (rejected or failed): read and discard the bytes, so the
                // client can finish sending and then read the response instead of hitting a connection reset.
                if (request.FileLength != null && !context.FileConsumed)
                {
                    await Wire.ReadFile(stream, Stream.Null, request.FileLength.Value);
                }
            }
            catch (Exception ex)
            {
                Log(remote + ": connection error (" + ex.GetType().Name + ": " + ex.Message + ")");
            }
        }
    }

    private async Task<RequestContext> Dispatch(Request request, Stream stream, ServerConfig config, string remote)
    {
        RequestContext context = new RequestContext(request, stream, config, Sessions);
        try
        {
            await handler.Handle(context);
            if (!context.ResponseSent)
            {
                Log(remote + ": " + request.Type + " handler sent no response.");
                await context.Fail(ResponseStatus.Error, "Server error.");
            }
        }
        catch (InvalidDataException ex)
        {
            Log(remote + ": " + request.Type + " invalid request: " + ex.Message);
            if (!context.ResponseSent)
            {
                await context.Fail(ResponseStatus.Error, "Invalid request: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            // Details go to the server log only; the client gets a generic message.
            Log(remote + ": " + request.Type + " failed: " + ex);
            if (!context.ResponseSent)
            {
                await context.Fail(ResponseStatus.Error, "Server error.");
            }
        }

        // One line per request, e.g. "192.168.1.5:50122  SubmitAssignment -> Ok (user 2)".
        Session? session = context.LoadedSession;
        string user = session != null ? "user " + session.UserId : "no session";
        Log(remote + "  " + request.Type + " -> " + context.SentStatus + " (" + user + ")");
        return context;
    }

    private void Log(string message)
    {
        EventHandler<string>? logHandler = LogMessage;
        if (logHandler != null)
        {
            logHandler(this, message);
        }
    }
}
