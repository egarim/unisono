using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using Unisono;

namespace Unisono.UI.Catalog;

/// <summary>
/// Turns an existing Unisono tool into a catalog document: one field per parameter, a submit
/// button bound to the tool, and a result area. The tool's MCP input schema is the source of truth,
/// so descriptions, required parameters, enums, formats and defaults carry over without duplication.
/// </summary>
public sealed class UnisonoUiDocumentBuilder
{
    private readonly ToolRegistry _tools;
    private readonly string _toolName;
    private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);
    private string? _title;
    private string? _submitLabel;

    public UnisonoUiDocumentBuilder(ToolRegistry tools, string toolName)
    {
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        _toolName = toolName;
    }

    /// <summary>Starts a document for the tool named <paramref name="toolName"/>.</summary>
    public static UnisonoUiDocumentBuilder For(ToolRegistry tools, string toolName) => new(tools, toolName);

    public string ToolName => _toolName;

    /// <summary>Overrides the title shown above the form. Defaults to the tool's title or its name.</summary>
    public UnisonoUiDocumentBuilder Title(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        _title = title;
        return this;
    }

    /// <summary>Overrides the submit button label. Defaults to "Run".</summary>
    public UnisonoUiDocumentBuilder SubmitLabel(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _submitLabel = label;
        return this;
    }

    /// <summary>Overrides one field label. Defaults to the parameter description.</summary>
    public UnisonoUiDocumentBuilder Label(string parameter, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _labels[parameter] = label;
        return this;
    }

    /// <summary>Builds and validates the document.</summary>
    public UnisonoUiDocument Build()
    {
        var tool = _tools.McpTools.FirstOrDefault(t => string.Equals(t.ProtocolTool.Name, _toolName, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException(
                $"Unisono UI catalog: no tool named '{_toolName}'. Known tools: {string.Join(", ", _tools.Names)}.");

        var protocolTool = tool.ProtocolTool;
        using var schemaDocument = JsonDocument.Parse(protocolTool.InputSchema.GetRawText());
        var schema = schemaDocument.RootElement;
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Tool '{_toolName}' has no object input schema to build a form from.");
        }

        var required = new HashSet<string>(StringComparer.Ordinal);
        if (schema.TryGetProperty("required", out var requiredArray) && requiredArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in requiredArray.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    required.Add(item.GetString()!);
            }
        }

        var title = _title ?? protocolTool.Title ?? Humanize(_toolName);
        var components = new List<UnisonoUiComponent>();
        var children = new List<string>();
        var dataModel = new JsonObject();
        var arguments = new Dictionary<string, UnisonoUiValue>(StringComparer.Ordinal);

        components.Add(new UnisonoUiComponent { Id = "title", Component = "Text", Text = UnisonoUiValue.Of(title), Variant = "title" });
        children.Add("title");

        if (!string.IsNullOrWhiteSpace(protocolTool.Description) && !SameText(protocolTool.Description, title))
        {
            components.Add(new UnisonoUiComponent
            {
                Id = "description",
                Component = "Text",
                Text = UnisonoUiValue.Of(protocolTool.Description),
                Variant = "caption",
            });
            children.Add("description");
        }

        foreach (var property in properties.EnumerateObject())
        {
            var name = property.Name;
            var field = BuildField(name, property.Value, required.Contains(name), out var defaultNode);
            var id = "field-" + name;
            field = CopyWithId(field, id);
            components.Add(field);
            children.Add(id);
            arguments[name] = UnisonoUiValue.At("/" + EscapePointer(name));
            if (defaultNode is not null)
                dataModel[name] = defaultNode;
        }

        components.Add(new UnisonoUiComponent
        {
            Id = "submit",
            Component = "Button",
            Label = _submitLabel ?? "Run",
            Variant = "primary",
            Action = new UnisonoUiAction { Name = _toolName, Arguments = arguments },
        });
        children.Add("submit");

        components.Add(new UnisonoUiComponent
        {
            Id = "result",
            Component = "Text",
            Text = UnisonoUiValue.At("/_result/text"),
            Variant = "body",
        });
        children.Add("result");

        components.Add(new UnisonoUiComponent
        {
            Id = "result-table",
            Component = "Table",
            Rows = UnisonoUiValue.At("/_result/rows"),
        });
        children.Add("result-table");

        components.Add(new UnisonoUiComponent
        {
            Id = "root",
            Component = "Column",
            Children = children,
            Gap = "md",
        });
        // Keep root first so the document reads top-down.
        var root = components[^1];
        components.RemoveAt(components.Count - 1);
        components.Insert(0, root);

        var document = UnisonoUiDocument.Surface(_toolName, components, dataModel);
        UnisonoUiDocumentValidator.ThrowIfInvalid(document);
        return document;
    }

    private UnisonoUiComponent BuildField(string name, JsonElement property, bool required, out JsonNode? defaultNode)
    {
        defaultNode = null;
        var description = property.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
            ? d.GetString()
            : null;
        var label = _labels.TryGetValue(name, out var custom) ? custom : description ?? Humanize(name);

        var types = new HashSet<string>(StringComparer.Ordinal);
        if (property.TryGetProperty("type", out var type))
        {
            if (type.ValueKind == JsonValueKind.String)
                types.Add(type.GetString()!);
            else if (type.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in type.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.String)
                        types.Add(entry.GetString()!);
                }
            }
        }

        types.Remove("null");

        if (property.TryGetProperty("default", out var defaultValue) && defaultValue.ValueKind != JsonValueKind.Null)
            defaultNode = JsonNode.Parse(defaultValue.GetRawText());

        if (property.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind == JsonValueKind.Array)
        {
            var options = new List<UnisonoUiOption>();
            foreach (var value in enumValues.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String)
                    throw Unsupported(name, "an enum whose values are not strings");
                var text = value.GetString()!;
                options.Add(new UnisonoUiOption(text, Humanize(text)));
            }

            return new UnisonoUiComponent
            {
                Id = name,
                Component = "Select",
                Label = label,
                Options = options,
                Required = required,
                Value = UnisonoUiValue.At("/" + EscapePointer(name)),
            };
        }

        if (types.Contains("boolean"))
        {
            return new UnisonoUiComponent
            {
                Id = name,
                Component = "Checkbox",
                Label = label,
                Required = required,
                Value = UnisonoUiValue.At("/" + EscapePointer(name)),
            };
        }

        if (types.Contains("integer") || types.Contains("number"))
        {
            var isInteger = types.Contains("integer");
            return new UnisonoUiComponent
            {
                Id = name,
                Component = "NumberInput",
                Label = label,
                Required = required,
                Step = isInteger ? 1 : 0.01,
                Min = TryDouble(property, "minimum"),
                Max = TryDouble(property, "maximum"),
                Value = UnisonoUiValue.At("/" + EscapePointer(name)),
            };
        }

        if (types.Contains("string"))
        {
            var format = property.TryGetProperty("format", out var f) && f.ValueKind == JsonValueKind.String
                ? f.GetString()
                : null;
            return new UnisonoUiComponent
            {
                Id = name,
                Component = "TextInput",
                Label = label,
                Placeholder = format == "date" ? "YYYY-MM-DD" : null,
                Format = format,
                Required = required,
                Value = UnisonoUiValue.At("/" + EscapePointer(name)),
            };
        }

        throw Unsupported(name, "a type the v1 catalog cannot render");
    }

    private NotSupportedException Unsupported(string parameter, string reason)
        => new($"Unisono UI catalog: parameter '{parameter}' of tool '{_toolName}' is {reason}. " +
               "Use a React, Blazor or Uno provider for this tool, or add a catalog component for the type.");

    private static UnisonoUiComponent CopyWithId(UnisonoUiComponent component, string id) => new()
    {
        Id = id,
        Component = component.Component,
        Label = component.Label,
        Placeholder = component.Placeholder,
        Format = component.Format,
        Required = component.Required,
        Step = component.Step,
        Min = component.Min,
        Max = component.Max,
        Options = component.Options,
        Value = component.Value,
        Text = component.Text,
        Children = component.Children,
        Action = component.Action,
        Rows = component.Rows,
        Columns = component.Columns,
        Items = component.Items,
        TitleField = component.TitleField,
        SubtitleField = component.SubtitleField,
        Src = component.Src,
        Alt = component.Alt,
        Variant = component.Variant,
        Gap = component.Gap,
    };

    private static double? TryDouble(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    internal static string EscapePointer(string segment) => segment.Replace("~", "~0").Replace("/", "~1");

    private static bool SameText(string left, string right)
    {
        static string Normalize(string value) => value.Trim().TrimEnd('.').Trim().ToLowerInvariant();
        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }

    internal static string Humanize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (current is '_' or '-')
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                    builder.Append(' ');
                continue;
            }

            if (i > 0 && char.IsUpper(current) && !char.IsUpper(name[i - 1]) && builder.Length > 0 && builder[^1] != ' ')
                builder.Append(' ');

            builder.Append(i == 0 ? char.ToUpperInvariant(current) : char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
