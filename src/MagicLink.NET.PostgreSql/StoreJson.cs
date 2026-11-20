using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace MagicLink.PostgreSql;

/// <summary>Converts the store's jsonb columns, without reflection, so it stays Native AOT safe.</summary>
internal static class StoreJson
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static string? WriteProperties(ImmutableDictionary<string, JsonElement> properties)
    {
        if (properties.IsEmpty)
        {
            return null;
        }

        return Write(properties, static (writer, values) =>
        {
            foreach (var (name, value) in values)
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }
        });
    }

    internal static ImmutableDictionary<string, JsonElement> ReadProperties(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return ImmutableDictionary<string, JsonElement>.Empty;
        }

        using var document = JsonDocument.Parse(json);
        var builder = ImmutableDictionary.CreateBuilder<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            builder[property.Name] = property.Value.Clone();
        }

        return builder.ToImmutable();
    }

    internal static string? WriteStrings(ImmutableDictionary<string, string> values)
    {
        if (values.IsEmpty)
        {
            return null;
        }

        return Write(values, static (writer, pairs) =>
        {
            foreach (var (name, value) in pairs)
            {
                writer.WriteString(name, value);
            }
        });
    }

    internal static ImmutableDictionary<string, string> ReadStrings(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return ImmutableDictionary<string, string>.Empty;
        }

        using var document = JsonDocument.Parse(json);
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            builder[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return builder.ToImmutable();
    }

    /// <summary>Localized names and descriptions: a JSON object keyed by culture name.</summary>
    internal static string? WriteLocalized(ImmutableDictionary<CultureInfo, string> values)
    {
        if (values.IsEmpty)
        {
            return null;
        }

        return Write(values, static (writer, pairs) =>
        {
            foreach (var (culture, value) in pairs)
            {
                writer.WriteString(culture.Name, value);
            }
        });
    }

    internal static ImmutableDictionary<CultureInfo, string> ReadLocalized(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return ImmutableDictionary<CultureInfo, string>.Empty;
        }

        using var document = JsonDocument.Parse(json);
        var builder = ImmutableDictionary.CreateBuilder<CultureInfo, string>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            builder[CultureInfo.GetCultureInfo(property.Name)] = property.Value.GetString() ?? string.Empty;
        }

        return builder.ToImmutable();
    }

    internal static string? WriteKeySet(JsonWebKeySet? set) =>
        set is null ? null : JsonSerializer.Serialize(set, OpenIddictSerializer.Default.JsonWebKeySet);

    internal static JsonWebKeySet? ReadKeySet(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonWebKeySet.Create(json);

    private static string Write<T>(T state, Action<Utf8JsonWriter, T> writeProperties)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writeProperties(writer, state);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
