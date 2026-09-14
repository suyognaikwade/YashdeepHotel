using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yashdeep.Shared.Serialization;

/// <summary>
/// Custom JsonConverter to enforce ISO 8601 UTC timestamp format during serialization and deserialization.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.TryGetDateTime(out var dateTime))
        {
            return dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).ToUniversalTime();
        }

        throw new JsonException("Invalid DateTime format in JSON.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utcValue = value.Kind == DateTimeKind.Utc
            ? value
            : value.ToUniversalTime();

        writer.WriteStringValue(utcValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}

/// <summary>
/// Central authoritative System.Text.Json configuration options for Yashdeep platform.
/// Enforces camelCase, ISO 8601 UTC timestamps, string enum representation, and strongly typed ID converters.
/// </summary>
public static class YashdeepJsonSerializerOptions
{
    private static readonly Lazy<JsonSerializerOptions> _defaultOptions = new(CreateDefaultOptions);

    /// <summary>
    /// Gets the shared immutable JsonSerializerOptions configured for platform standards.
    /// </summary>
    public static JsonSerializerOptions Default => _defaultOptions.Value;

    /// <summary>
    /// Creates a new instance of JsonSerializerOptions initialized with standard platform converters.
    /// </summary>
    public static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new UtcDateTimeJsonConverter());
        options.Converters.Add(new StronglyTypedIdJsonConverterFactory());

        return options;
    }
}
