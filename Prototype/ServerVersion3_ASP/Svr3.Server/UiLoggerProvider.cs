namespace Svr3.Server;

/// <summary>
/// Forwards server log lines to the window. Raised on Kestrel/thread-pool threads,
/// so the subscriber must marshal to the UI thread.
/// </summary>
public sealed class UiLoggerProvider : ILoggerProvider
{
    public event Action<string>? LineWritten;

    public ILogger CreateLogger(string categoryName)
    {
        return new UiLogger(this, categoryName);
    }

    public void Dispose()
    {
    }

    private void Write(string line)
    {
        LineWritten?.Invoke(line);
    }

    private sealed class UiLogger : ILogger
    {
        private readonly UiLoggerProvider _provider;
        private readonly string _category;

        public UiLogger(UiLoggerProvider provider, string categoryName)
        {
            _provider = provider;
            _category = categoryName.Substring(categoryName.LastIndexOf('.') + 1);
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        /// <summary>Levels are already filtered by the "Logging" section of appsettings.json.</summary>
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " [" + logLevel + "] " + _category + ": " + formatter(state, exception);
            if (exception != null)
            {
                line += " | " + exception.GetType().Name + ": " + exception.Message;
            }
            _provider.Write(line);
        }
    }
}
