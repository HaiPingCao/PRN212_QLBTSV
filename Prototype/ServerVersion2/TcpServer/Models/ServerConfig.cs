using System.Text.Json;
using Tcp.Shared;

namespace Server.Models;

/// <summary>
/// Server settings ("Cấu hình máy chủ"), stored as JSON next to the server executable.
/// Changes take effect on the next TcpServer.Start().
/// </summary>
public sealed class ServerConfig
{
    public static readonly string DefaultFilePath = Path.Combine(AppContext.BaseDirectory, "server-config.json");
    public int Port { get; set; } = Wire.DefaultPort;
    public string StoragePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "storage");

    /// <summary>A session expires after this many minutes without requests.</summary>
    public int SessionTimeoutMinutes { get; set; } = 120;

    /// <summary>A connection is dropped if its request message doesn't arrive within this time.</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>Loads the file, or creates it with defaults if it doesn't exist.</summary>
    /// <exception cref="InvalidDataException">File is not valid JSON or has invalid values.</exception>
    public static ServerConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            ServerConfig defaults = new ServerConfig();
            defaults.Save(path);
            return defaults;
        }

        ServerConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Config file is not valid JSON: " + ex.Message, ex);
        }

        if (config == null)
        {
            throw new InvalidDataException("Config file is empty.");
        }
        config.Validate();
        return config;
    }

    /// <exception cref="InvalidDataException">A value is invalid; nothing is written.</exception>
    public void Save(string path)
    {
        Validate();
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.WriteIndented = true;
        File.WriteAllText(path, JsonSerializer.Serialize(this, options));
    }

    /// <exception cref="InvalidDataException">Message names the first invalid setting.</exception>
    public void Validate()
    {
        if (Port < 1 || Port > 65535)
        {
            throw new InvalidDataException("Port must be between 1 and 65535.");
        }
        if (string.IsNullOrWhiteSpace(StoragePath) || !Path.IsPathFullyQualified(StoragePath))
        {
            throw new InvalidDataException("Storage path must be an absolute path.");
        }
        //if (string.IsNullOrWhiteSpace(ConnectionString))
        //{
        //    throw new InvalidDataException("Connection string is required.");
        //}
        if (SessionTimeoutMinutes < 1)
        {
            throw new InvalidDataException("Session timeout must be at least 1 minute.");
        }
        if (RequestTimeoutSeconds < 1)
        {
            throw new InvalidDataException("Request timeout must be at least 1 second.");
        }
    }

    public ServerConfig Clone()
    {
        return (ServerConfig)MemberwiseClone();
    }
}
