using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using Android.App.AppFunctions;
using Android.App.AppSearch;

namespace Unisono.AppFunctions;

/// <summary>
/// Converts between AppSearch <see cref="GenericDocument"/>s (what AppFunctions carry) and the JSON the registry speaks.
/// Every GenericDocument property is an array; a one-element array is read as a scalar, which matches how callers
/// (Gemini, <c>cmd app_function execute-app-function</c>) pass scalar parameters. Objects become nested documents.
/// </summary>
[SupportedOSPlatform("android36.0")]
public static class GenericDocumentJson
{
    /// <summary>Schema type used for documents built from JSON objects.</summary>
    public const string ObjectSchemaType = "UnisonoObject";

    public static JsonObject ToJsonObject(GenericDocument? document)
    {
        var json = new JsonObject();
        if (document is null) return json;
        foreach (var name in document.PropertyNames)
        {
            var node = ToJsonNode(document, name);
            if (node is not null) json[name] = node;
        }
        // Callers that build the document from JSON (the `cmd app_function` shell, for one) treat a top-level "id"
        // key as the document id rather than a property. Hand it to a tool parameter named "id" when none arrived.
        if (!json.ContainsKey("id") && !string.IsNullOrEmpty(document.Id)) json["id"] = document.Id;
        return json;
    }

    private static JsonNode? ToJsonNode(GenericDocument document, string name)
    {
        var strings = document.GetPropertyStringArray(name);
        if (strings is not null) return Scalarize(strings.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray());
        var longs = document.GetPropertyLongArray(name);
        if (longs is not null) return Scalarize(longs.Select(l => (JsonNode?)JsonValue.Create(l)).ToArray());
        var doubles = document.GetPropertyDoubleArray(name);
        if (doubles is not null) return Scalarize(doubles.Select(d => (JsonNode?)JsonValue.Create(d)).ToArray());
        var bools = document.GetPropertyBooleanArray(name);
        if (bools is not null) return Scalarize(bools.Select(b => (JsonNode?)JsonValue.Create(b)).ToArray());
        var documents = document.GetPropertyDocumentArray(name);
        if (documents is not null) return Scalarize(documents.Select(d => (JsonNode?)ToJsonObject(d)).ToArray());
        return null;
    }

    private static JsonNode? Scalarize(JsonNode?[] items)
        => items.Length == 1 ? items[0] : new JsonArray(items);

    /// <summary>Wraps a tool result in the response document the platform expects.</summary>
    public static GenericDocument ToResultDocument(JsonNode? result)
    {
        var builder = new GenericDocument.Builder("", "", "UnisonoResult");
        if (result is not null) SetProperty(builder, ExecuteAppFunctionResponse.PropertyReturnValue, result);
        return builder.Build();
    }

    public static GenericDocument ToDocument(JsonObject json, string schemaType = ObjectSchemaType)
    {
        var builder = new GenericDocument.Builder("", "", schemaType);
        foreach (var (name, value) in json)
        {
            if (value is not null) SetProperty(builder, name, value);
        }
        return builder.Build();
    }

    private static void SetProperty(GenericDocument.Builder builder, string name, JsonNode value)
    {
        switch (value)
        {
            case JsonObject o:
                builder.SetPropertyDocument(name, [ToDocument(o)]);
                break;
            case JsonArray a:
                SetArray(builder, name, a);
                break;
            case JsonValue v:
                SetScalar(builder, name, v);
                break;
        }
    }

    private static void SetScalar(GenericDocument.Builder builder, string name, JsonValue v)
    {
        var element = v.GetValue<JsonElement>();
        switch (element.ValueKind)
        {
            case JsonValueKind.String: builder.SetPropertyString(name, [element.GetString()!]); break;
            case JsonValueKind.True: builder.SetPropertyBoolean(name, [true]); break;
            case JsonValueKind.False: builder.SetPropertyBoolean(name, [false]); break;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l)) builder.SetPropertyLong(name, [l]);
                else builder.SetPropertyDouble(name, [element.GetDouble()]);
                break;
            default: break; // null and undefined: no property
        }
    }

    private static void SetArray(GenericDocument.Builder builder, string name, JsonArray a)
    {
        var items = a.Where(x => x is not null).ToList();
        if (items.Count == 0) { builder.SetPropertyString(name, []); return; }
        if (items.All(x => x is JsonObject))
        {
            builder.SetPropertyDocument(name, items.Select(x => ToDocument((JsonObject)x!)).ToArray());
            return;
        }
        var elements = items.Select(x => x is JsonValue v ? v.GetValue<JsonElement>() : default).ToList();
        if (elements.All(e => e.ValueKind == JsonValueKind.String))
        {
            builder.SetPropertyString(name, elements.Select(e => e.GetString()!).ToArray());
        }
        else if (elements.All(e => e.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            builder.SetPropertyBoolean(name, elements.Select(e => e.GetBoolean()).ToArray());
        }
        else if (elements.All(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out _)))
        {
            builder.SetPropertyLong(name, elements.Select(e => e.GetInt64()).ToArray());
        }
        else if (elements.All(e => e.ValueKind == JsonValueKind.Number))
        {
            builder.SetPropertyDouble(name, elements.Select(e => e.GetDouble()).ToArray());
        }
        else
        {
            builder.SetPropertyString(name, items.Select(x => x!.ToJsonString()).ToArray());
        }
    }
}
