using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TerraPDF.Agents.Spec;

/// <summary>
/// Parses <see cref="PdfDocumentSpec"/> JSON. Lenient where LLMs commonly
/// drift (property casing, comments, trailing commas, numbers where strings are
/// expected) and strict where drift silently loses content (unknown properties
/// are errors, so a misspelt <c>"colour"</c> is reported instead of ignored).
/// </summary>
public static class PdfSpecReader
{
    /// <summary>Serializer options used for every spec read and write.</summary>
    public static JsonSerializerOptions SerializerOptions { get; } = CreateOptions();

    /// <summary>Parses a JSON string. Returns <c>null</c> and populates <paramref name="error"/> on failure.</summary>
    public static PdfDocumentSpec? TryRead(string json, out SpecIssue? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = new SpecIssue("$", "The document JSON is empty.");
            return null;
        }

        try
        {
            var spec = JsonSerializer.Deserialize<PdfDocumentSpec>(json, SerializerOptions);
            if (spec is null) error = new SpecIssue("$", "The document JSON is null; expected an object.");
            return spec;
        }
        catch (JsonException ex)
        {
            error = new SpecIssue(ex.Path ?? "$", Describe(ex));
            return null;
        }
    }

    /// <summary>Parses an already-parsed JSON value, as handed over by a tool-calling framework.</summary>
    public static PdfDocumentSpec? TryRead(JsonElement json, out SpecIssue? error)
    {
        // Some models send the document as a JSON-encoded string instead of an
        // object; accept both rather than failing on a formatting choice.
        if (json.ValueKind == JsonValueKind.String)
            return TryRead(json.GetString() ?? string.Empty, out error);

        if (json.ValueKind != JsonValueKind.Object)
        {
            error = new SpecIssue("$", $"Expected a JSON object for the document, got {json.ValueKind}.");
            return null;
        }

        return TryRead(json.GetRawText(), out error);
    }

    private static string Describe(JsonException ex)
    {
        // System.Text.Json messages name CLR types ("TerraPDF.Agents.Spec.BlockSpec"),
        // which mean nothing to a model; strip them down to the actionable part.
        string message = ex.Message;
        int pathIndex = message.IndexOf(" Path:", StringComparison.Ordinal);
        if (pathIndex > 0) message = message[..pathIndex];
        return message
            .Replace("TerraPDF.Agents.Spec.", string.Empty, StringComparison.Ordinal)
            .Replace("System.", string.Empty, StringComparison.Ordinal);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.Converters.Add(new LenientStringConverter());
        options.MakeReadOnly();
        return options;
    }

    /// <summary>
    /// Accepts numbers and booleans wherever a string is expected. Table rows are
    /// the common case: models write <c>["Widget", 2, 9.99]</c>, not all strings.
    /// </summary>
    private sealed class LenientStringConverter : JsonConverter<string?>
    {
        public override bool HandleNull => true;

        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => System.Text.Encoding.UTF8.GetString(reader.ValueSpan),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                JsonTokenType.Null => null,
                _ => throw new JsonException($"Expected a string but found {reader.TokenType}."),
            };

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(value);
        }
    }
}

/// <summary>A validation error or warning located by a JSON path such as <c>$.content[3].rows[1]</c>.</summary>
public sealed record SpecIssue(string Path, string Message)
{
    public override string ToString() => $"{Path}: {Message}";
}
