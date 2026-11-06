using System.Text.Json;

namespace Tcp.Shared;

internal static class BodyReader
{
    public static TBody Read<TBody>(JsonElement? body)
    {
        if (body == null)
        {
            throw new InvalidDataException("Message has no body.");
        }

        TBody? value;
        try
        {
            value = body.Value.Deserialize<TBody>(Wire.JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Body is not a valid " + typeof(TBody).Name + ".", ex);
        }

        if (value == null)
        {
            throw new InvalidDataException("Body is null.");
        }
        return value;
    }
}
