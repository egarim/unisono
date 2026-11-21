using System.Text;
using Unisono.McpApps;

namespace Unisono.UI.Catalog;

/// <summary>
/// A UI provider that serves generated documents with the generic catalog renderer. Register it
/// once, map any number of tools to it, and every tool gets an interface without a frontend project.
/// </summary>
public sealed class CatalogUiProvider : IUnisonoUiProvider
{
    /// <summary>The framework name to pass to <c>UnisonoUiRegistry.MapTool</c>.</summary>
    public const string FrameworkName = "catalog";

    private readonly Dictionary<string, UnisonoUiDocument> _documents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _titles = new(StringComparer.Ordinal);

    public string Framework => FrameworkName;

    /// <summary>Adds an explicitly authored document for a resource URI.</summary>
    public CatalogUiProvider Add(string resourceUri, UnisonoUiDocument document, string? title = null)
    {
        ValidateResourceUri(resourceUri);
        ArgumentNullException.ThrowIfNull(document);
        UnisonoUiDocumentValidator.ThrowIfInvalid(document);
        if (!_documents.TryAdd(resourceUri, document))
            throw new InvalidOperationException($"A document is already registered for '{resourceUri}'.");
        _titles[resourceUri] = title;
        return this;
    }

    /// <summary>Generates the document from a tool's MCP input schema and adds it.</summary>
    public CatalogUiProvider AddFromTool(
        ToolRegistry tools,
        string toolName,
        string resourceUri,
        Action<UnisonoUiDocumentBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(tools);
        var builder = UnisonoUiDocumentBuilder.For(tools, toolName);
        configure?.Invoke(builder);
        return Add(resourceUri, builder.Build(), builder.ToolName);
    }

    /// <summary>The document registered for a resource URI, when there is one.</summary>
    public bool TryGetDocument(string resourceUri, out UnisonoUiDocument? document)
        => _documents.TryGetValue(resourceUri, out document);

    public ValueTask<UnisonoUiAsset> GetAssetAsync(UnisonoUiDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(definition.Framework, Framework, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("UI resource belongs to another framework.", nameof(definition));
        if (!_documents.TryGetValue(definition.ResourceUri, out var document))
            throw new KeyNotFoundException($"No UI document registered for '{definition.ResourceUri}'.");

        _titles.TryGetValue(definition.ResourceUri, out var title);
        var html = UnisonoUiRenderer.Render(document, title);
        return ValueTask.FromResult(new UnisonoUiAsset(
            definition.ResourceUri,
            "text/html;profile=mcp-app",
            Encoding.UTF8.GetBytes(html)));
    }

    private static void ValidateResourceUri(string resourceUri)
    {
        if (!Uri.TryCreate(resourceUri, UriKind.Absolute, out var uri) || uri.Scheme != "ui")
            throw new ArgumentException("Expected a ui:// resource URI.", nameof(resourceUri));
    }
}
