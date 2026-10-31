namespace Svr3.Server.Files;

/// <summary>
/// Streams an upload into a temp file. The caller moves it over the real file only after the DB
/// update succeeds, so a dropped connection or a failed save keeps the old file.
/// </summary>
public static class FileReceiver
{
    /// <returns>Path of the complete temp file (in <paramref name="directory"/>) and its length.</returns>
    public static async Task<(string TempPath, long Length)> ReceiveToTemp(Stream body, string directory, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        string tempPath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (FileStream file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await body.CopyToAsync(file, 81920, ct);
                return (tempPath, file.Length);
            }
        }
        catch
        {
            DeleteQuietly(tempPath);
            throw;
        }
    }

    public static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Strips any directory part. Returns null if nothing usable is left.</summary>
    public static string? CleanFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string name = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
        if (name.Length == 0 || name.Length > 255 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }
        return name;
    }
}
