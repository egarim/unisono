namespace Unisono.UI.Catalog;

/// <summary>
/// The component vocabulary and document version this package emits. The catalog id is the
/// "language" of a document: a renderer only draws components it knows, and the agent can only
/// compose what the catalog allows.
/// </summary>
public static class UnisonoUiCatalog
{
    /// <summary>Identifies the Unisono catalog. Change it when the component set breaks compatibility.</summary>
    public const string Id = "https://unisono.dev/ui/catalog/v1";

    /// <summary>Document envelope version. Independent from the catalog id.</summary>
    public const string Version = "1.0";

    /// <summary>The component names a v1 renderer understands.</summary>
    public static IReadOnlySet<string> Components { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Column",
        "Row",
        "Text",
        "TextInput",
        "NumberInput",
        "Select",
        "Checkbox",
        "Button",
        "List",
        "Table",
        "Image",
        "Divider",
    };

    /// <summary>True when <paramref name="component"/> can be rendered by the v1 catalog.</summary>
    public static bool IsComponent(string component) => Components.Contains(component);
}
