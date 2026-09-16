using System.Text.Json;
using System.Text.Json.Serialization;

namespace Athkar.Shareds.Json;

/// <summary>
/// Normalises every inbound <see cref="DateTime"/> to UTC and stamps every
/// outbound one as UTC.
///
/// Everything this server stores is UTC, but a JSON date with no offset
/// deserialises as <see cref="DateTimeKind.Unspecified"/> and is then compared
/// against <c>DateTime.UtcNow</c> as though it were already UTC — which is right
/// exactly when the client happened to send UTC. Making the conversion explicit
/// here means a client may send an offset and be understood, and a client that
/// sends none is taken at its word rather than by accident.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

public class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly UtcDateTimeConverter Inner = new();

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : Inner.Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else Inner.Write(writer, value.Value, options);
    }
}
