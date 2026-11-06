using System.IO;
using System.Text.Json;

namespace QLBT.Server.Asp
{
    public sealed class ServerSettings
    {
        public const int DefaultPort = 5080;

        private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QLBT.Server", "settings.json");

        /// <summary>IP to listen on. 0.0.0.0 = every network interface.</summary>
        public string BindAddress { get; set; } = "0.0.0.0";

        public int Port { get; set; } = DefaultPort;

        /// <summary>Absolute path of the storage root (see FileStorage).</summary>
        public string StoragePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "storage");

        /// <summary>Start the server as soon as the app opens; otherwise wait for the Start button.</summary>
        public bool AutoStart { get; set; } = true;

        public static ServerSettings Load()
        {
            if (!File.Exists(FilePath))
            {
                return new ServerSettings();
            }

            try
            {
                return JsonSerializer.Deserialize<ServerSettings>(File.ReadAllText(FilePath)) ?? new ServerSettings();
            }
            catch (JsonException)
            {
                return new ServerSettings();
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}

