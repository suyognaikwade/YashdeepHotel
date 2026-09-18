using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Yashdeep.Shared.Identifiers;

namespace Yashdeep.Shared.Serialization;

/// <summary>
/// Generic JsonConverter for strongly typed identifiers implementing IStronglyTypedId.
/// Serializes to a standard hyphenated Guid string ("D") and deserializes from Guid string.
/// </summary>
/// <typeparam name="TId">The strongly typed ID type.</typeparam>
public class StronglyTypedIdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct, IStronglyTypedId
{
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (Guid.TryParse(stringValue, out var parsedGuid))
            {
                return (TId)Activator.CreateInstance(typeof(TId), parsedGuid)!;
            }
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        throw new JsonException($"Unable to convert JSON token '{reader.TokenType}' to strongly typed ID '{typeof(TId).Name}'.");
    }

    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value.ToString("D"));
    }
}

/// <summary>
/// JsonConverterFactory that dynamically creates StronglyTypedIdJsonConverter for any type implementing IStronglyTypedId.
/// </summary>
public class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(IStronglyTypedId).IsAssignableFrom(typeToConvert) && !typeToConvert.IsAbstract && !typeToConvert.IsInterface;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(StronglyTypedIdJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}
