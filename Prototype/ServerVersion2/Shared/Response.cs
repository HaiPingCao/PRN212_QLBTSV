using System.Text.Json;

namespace Tcp.Shared;

public sealed class Response
{
    /// <summary>Same as the request's Type.</summary>
    public MessageType Type { get; set; }

    public ResponseStatus Status { get; set; }

    /// <summary>Human-readable reason when Status is not Ok; otherwise null.</summary>
    public string? Error { get; set; }

    /// <summary>Set only by a successful Login.</summary>
    public string? SessionId { get; set; }

    /// <summary>Operation-specific result. Null on failure or for operations without a result.</summary>
    public JsonElement? Body { get; set; }

    /// <summary>Set when exactly this many file bytes follow the message (downloads). Null = no file.</summary>
    public long? FileLength { get; set; }

    public bool IsOk
    {
        get { return Status == ResponseStatus.Ok; }
    }

    public static Response Ok(MessageType type)
    {
        Response response = new Response();
        response.Type = type;
        response.Status = ResponseStatus.Ok;
        return response;
    }

    public static Response Ok<TBody>(MessageType type, TBody body)
    {
        Response response = Ok(type);
        response.Body = JsonSerializer.SerializeToElement(body, Wire.JsonOptions);
        return response;
    }

    public static Response Fail(MessageType type, ResponseStatus status, string error)
    {
        if (status == ResponseStatus.Ok)
        {
            throw new ArgumentException("Use Ok() for success.", nameof(status));
        }

        Response response = new Response();
        response.Type = type;
        response.Status = status;
        response.Error = error;
        return response;
    }

    /// <exception cref="InvalidDataException">Body is missing or doesn't match TBody.</exception>
    public TBody GetBody<TBody>()
    {
        return BodyReader.Read<TBody>(Body);
    }
}
