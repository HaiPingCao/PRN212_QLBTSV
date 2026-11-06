namespace Svr3.Server;

/// <summary>
/// Starts and stops the HTTP server. Each Start builds a new host from the current settings,
/// so a changed IP, port or storage path applies on the next Start.
/// Sessions are in memory: stopping the server logs every client out.
/// </summary>
public sealed class ServerRunner
{
    private readonly ILoggerProvider _log;
    private WebApplication? _app;

    public ServerRunner(ILoggerProvider log)
    {
        _log = log;
    }

    public bool IsRunning
    {
        get { return _app != null; }
    }

    /// <exception cref="IOException">Address or port cannot be bound (in use, not on this machine).</exception>
    public async Task Start(ServerSettings settings)
    {
        if (_app != null)
        {
            return;
        }

        WebApplication app = ServerHost.Build(settings, _log);
        try
        {
            await app.StartAsync();
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
        _app = app;
    }

    public async Task Stop()
    {
        if (_app == null)
        {
            return;
        }

        WebApplication app = _app;
        _app = null;
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
