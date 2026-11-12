using System.IO;
using System.Text.Json;
using QLBT.Shared;

namespace QLBT.Client;

/// <summary>
/// Settings from appsettings.json next to the exe: { "ServerUrl": "http://192.168.1.10:5080" }.
/// A missing file, key or bad URL falls back to <see cref="ApiRoutes.DefaultBaseUrl"/>.
/// </summary>
public sealed class ClientSettings
{
    public string ServerUrl { get; set; } = ApiRoutes.DefaultBaseUrl;

    public static ClientSettings Load()
    {
        ClientSettings settings = new ClientSettings();
        string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        try
        {
            if (File.Exists(path))
            {
                JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                settings = JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(path), options) ?? settings;
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        if (!Uri.TryCreate(settings.ServerUrl, UriKind.Absolute, out _))
        {
            settings.ServerUrl = ApiRoutes.DefaultBaseUrl;
        }
        return settings;
    }
}
