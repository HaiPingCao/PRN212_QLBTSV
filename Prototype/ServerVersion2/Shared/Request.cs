using System.Text.Json;

namespace Tcp.Shared;

public sealed class Request
{
    public MessageType Type { get; set; }

    /// <summary>Null before login.</summary>
    public string? SessionId { get; set; }

    /// <summary>Operation-specific data. Null for operations without a body.</summary>
    public JsonElement? Body { get; set; }

    /// <summary>Set when exactly this many file bytes follow the message (uploads). Null = no file.</summary>
    public long? FileLength { get; set; }

    public static Request Create(MessageType type, string? sessionId)
    {
        Request request = new Request();
        request.Type = type;
        request.SessionId = sessionId;
        return request;
    }

    public static Request Create<TBody>(MessageType type, string? sessionId, TBody body)
    {
        Request request = Create(type, sessionId);
        request.Body = JsonSerializer.SerializeToElement(body, Wire.JsonOptions);
        return request;
    }

    /// <exception cref="InvalidDataException">Body is missing or doesn't match TBody.</exception>
    public TBody GetBody<TBody>()
    {
        return BodyReader.Read<TBody>(Body);
    }
}
