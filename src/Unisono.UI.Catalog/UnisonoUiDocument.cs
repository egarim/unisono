using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Unisono.UI.Catalog;

/// <summary>
/// A literal value or a data binding inside a UI document. A binding is written as
/// <c>{"@path": "/quantity"}</c> (JSON Pointer into the document's data model).
/// </summary>
[JsonConverter(typeof(UnisonoUiValueConverter))]
public sealed class UnisonoUiValue
{
    private readonly JsonNode? _literal;

    private UnisonoUiValue(string? path, JsonNode? literal)
    {
        Path = path;
        _literal = literal;
    }

    /// <summary>The JSON Pointer this value reads from, or <c>null</c> for a literal.</summary>
    public string? Path { get; }

    /// <summary>True when the value is a data binding.</summary>
    public bool IsPath => Path is not null;

    /// <summary>The literal JSON node, or <c>null</c> for a binding.</summary>
    public JsonNode? Literal => _literal;

    /// <summary>Binds the value to a JSON Pointer such as <c>/quantity</c>.</summary>
    public static UnisonoUiValue At(string pointer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pointer);
        if (pointer[0] != '/')
            throw new ArgumentException($"A data binding must be a JSON Pointer that starts with '/': '{pointer}'.", nameof(pointer));
        return new UnisonoUiValue(pointer, null);
    }

    /// <summary>A literal string.</summary>
    public static UnisonoUiValue Of(string value) => new(null, JsonValue.Create(value));

    /// <summary>A literal boolean.</summary>
    public static UnisonoUiValue Of(bool value) => new(null, JsonValue.Create(value));

    /// <summary>A literal number.</summary>
    public static UnisonoUiValue Of(double value) => new(null, JsonValue.Create(value));

    /// <summary>A literal number.</summary>
    public static UnisonoUiValue Of(long value) => new(null, JsonValue.Create(value));

    /// <summary>A literal copied from an existing JSON node.</summary>
    public static UnisonoUiValue From(JsonNode? node) => new(null, node?.DeepClone());

    internal JsonNode? ToJsonNode() => Path is null ? _literal?.DeepClone() : new JsonObject { ["@path"] = Path };
}

/// <summary>Serialises <see cref="UnisonoUiValue"/> as a JSON literal or <c>{"@path": "..."}</c>.</summary>
internal sealed class UnisonoUiValueConverter : JsonConverter<UnisonoUiValue>
{
    public override UnisonoUiValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("@path", out var path) &&
            path.ValueKind == JsonValueKind.String)
        {
            return UnisonoUiValue.At(path.GetString()!);
        }

        return UnisonoUiValue.From(JsonNode.Parse(root.GetRawText()));
    }

    public override void Write(Utf8JsonWriter writer, UnisonoUiValue value, JsonSerializerOptions options)
    {
        var node = value.ToJsonNode();
        if (node is null)
            writer.WriteNullValue();
        else
            node.WriteTo(writer);
    }
}

/// <summary>One UI document: an A2UI-shaped envelope with a fixed Unisono catalog.</summary>
public sealed class UnisonoUiDocument
{
    /// <summary>Envelope version; <see cref="UnisonoUiCatalog.Version"/>.</summary>
    public string Version { get; init; } = UnisonoUiCatalog.Version;

    /// <summary>The single surface this document creates.</summary>
    public required UnisonoUiSurface CreateSurface { get; init; }

    /// <summary>Starts a document for one surface.</summary>
    public static UnisonoUiDocument Surface(string surfaceId, IReadOnlyList<UnisonoUiComponent> components, JsonObject? dataModel = null)
        => new()
        {
            CreateSurface = new UnisonoUiSurface
            {
                SurfaceId = surfaceId,
                CatalogId = UnisonoUiCatalog.Id,
                Components = components,
                DataModel = dataModel,
            },
        };
}

/// <summary>A surface: the component tree plus its data model.</summary>
public sealed class UnisonoUiSurface
{
    public required string SurfaceId { get; init; }

    public string CatalogId { get; init; } = UnisonoUiCatalog.Id;

    public required IReadOnlyList<UnisonoUiComponent> Components { get; init; }

    /// <summary>Initial data the components bind against. Keys are tool parameter names.</summary>
    public JsonObject? DataModel { get; init; }
}

/// <summary>One node of the component tree. Only the properties relevant to <see cref="Component"/> are set.</summary>
public sealed class UnisonoUiComponent
{
    public required string Id { get; init; }

    /// <summary>A name from <see cref="UnisonoUiCatalog.Components"/>.</summary>
    public required string Component { get; init; }

    public IReadOnlyList<string>? Children { get; init; }

    public UnisonoUiValue? Text { get; init; }

    public UnisonoUiValue? Value { get; init; }

    public string? Label { get; init; }

    public string? Placeholder { get; init; }

    /// <summary>Optional input format, for example <c>date</c> or <c>email</c>.</summary>
    public string? Format { get; init; }

    /// <summary><c>title</c>, <c>body</c> or <c>caption</c> for text, <c>primary</c>/<c>secondary</c> for buttons.</summary>
    public string? Variant { get; init; }

    /// <summary>Spacing hint for <c>Column</c> and <c>Row</c>: <c>sm</c>, <c>md</c> or <c>lg</c>.</summary>
    public string? Gap { get; init; }

    public bool? Required { get; init; }

    public double? Min { get; init; }

    public double? Max { get; init; }

    public double? Step { get; init; }

    public IReadOnlyList<UnisonoUiOption>? Options { get; init; }

    public UnisonoUiAction? Action { get; init; }

    public UnisonoUiValue? Rows { get; init; }

    public IReadOnlyList<UnisonoUiColumn>? Columns { get; init; }

    public UnisonoUiValue? Items { get; init; }

    public string? TitleField { get; init; }

    public string? SubtitleField { get; init; }

    public string? Src { get; init; }

    public string? Alt { get; init; }
}

/// <summary>One option of a <c>Select</c>.</summary>
public sealed record UnisonoUiOption(string Value, string Label);

/// <summary>One column of a <c>Table</c>.</summary>
public sealed record UnisonoUiColumn(string Key, string Label);

/// <summary>What a <c>Button</c> does: call a tool with bound arguments.</summary>
public sealed class UnisonoUiAction
{
    /// <summary>The MCP tool name.</summary>
    public required string Name { get; init; }

    /// <summary>Tool arguments, each a literal or a binding into the data model.</summary>
    public IReadOnlyDictionary<string, UnisonoUiValue>? Arguments { get; init; }
}

/// <summary>Serialisation helpers shared by the provider and the tests.</summary>
public static class UnisonoUiDocumentJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(UnisonoUiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static UnisonoUiDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<UnisonoUiDocument>(json, Options)
            ?? throw new InvalidDataException("The UI document is empty.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
        options.Converters.Add(new UnisonoUiValueConverter());
        return options;
    }
}
