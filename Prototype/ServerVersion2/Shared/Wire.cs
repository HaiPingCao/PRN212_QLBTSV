using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tcp.Shared;

/// <summary>
/// Reads and writes messages on a stream (normally a NetworkStream). One request per TCP connection.
///
/// Frame:  int32 length (little-endian) | UTF-8 JSON of Request or Response | [file bytes]
///
/// File bytes follow a message if and only if its FileLength is set, and there are exactly FileLength of them.
///
/// Client:  WriteRequest -> [WriteFile] -> ReadResponse -> [ReadFile]
/// Server:  ReadRequest  -> [ReadFile]  -> WriteResponse -> [WriteFile]
///
/// If the server rejects an upload, it still reads (and discards) the file bytes before closing,
/// so the client always finishes sending and then receives the response.
/// </summary>
public static class Wire
{
    public const int DefaultPort = 5000;

    /// <summary>Upper limit for one JSON message. Files are streamed separately and have no limit.</summary>
    public const int MaxMessageBytes = 1024 * 1024;

    /// <summary>Enums are written as names ("Login"), not numbers.</summary>
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static Task WriteRequest(Stream stream, Request request)
    {
        return WriteMessage(stream, request);
    }

    public static Task<Request> ReadRequest(Stream stream)
    {
        return ReadMessage<Request>(stream);
    }

    public static Task WriteResponse(Stream stream, Response response)
    {
        return WriteMessage(stream, response);
    }

    public static Task<Response> ReadResponse(Stream stream)
    {
        return ReadMessage<Response>(stream);
    }

    /// <summary>Sends exactly <paramref name="length"/> bytes from <paramref name="source"/>.</summary>
    public static async Task WriteFile(Stream stream, Stream source, long length)
    {
        await CopyExact(source, stream, length);
        await stream.FlushAsync();
    }

    /// <summary>Receives exactly <paramref name="length"/> bytes into <paramref name="destination"/>.</summary>
    /// <exception cref="EndOfStreamException">Connection closed before all bytes arrived.</exception>
    public static Task ReadFile(Stream stream, Stream destination, long length)
    {
        return CopyExact(stream, destination, length);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static async Task WriteMessage<TMessage>(Stream stream, TMessage message)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        if (json.Length > MaxMessageBytes)
        {
            throw new InvalidOperationException("Message is " + json.Length + " bytes; limit is " + MaxMessageBytes + ".");
        }

        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, json.Length);

        await stream.WriteAsync(header);
        await stream.WriteAsync(json);
        await stream.FlushAsync();
    }

    /// <exception cref="EndOfStreamException">Connection closed mid-message.</exception>
    /// <exception cref="InvalidDataException">Bad length or invalid JSON.</exception>
    private static async Task<TMessage> ReadMessage<TMessage>(Stream stream)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header);

        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaxMessageBytes)
        {
            throw new InvalidDataException("Invalid message length: " + length + ".");
        }

        byte[] json = new byte[length];
        await stream.ReadExactlyAsync(json);

        TMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<TMessage>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Invalid " + typeof(TMessage).Name + " JSON.", ex);
        }

        if (message == null)
        {
            throw new InvalidDataException("Empty " + typeof(TMessage).Name + ".");
        }
        return message;
    }

    private static async Task CopyExact(Stream source, Stream destination, long length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        byte[] buffer = new byte[81920];
        long remaining = length;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(buffer.Length, remaining);
            int read = await source.ReadAsync(buffer, 0, toRead);
            if (read == 0)
            {
                throw new EndOfStreamException("Stream ended with " + remaining + " of " + length + " bytes missing.");
            }
            await destination.WriteAsync(buffer, 0, read);
            remaining -= read;
        }
    }
}
