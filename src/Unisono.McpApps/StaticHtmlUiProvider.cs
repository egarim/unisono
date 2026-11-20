using System.Text;

namespace Unisono.McpApps;

/// <summary>
/// Minimal built-in provider for already-bundled HTML. Useful for testing the
/// provider abstraction and for integrations that do not need a frontend framework.
/// Content must be trusted, prebuilt UI supplied by the application developer.
/// </summary>
public sealed class StaticHtmlUiProvider : IUnisonoUiProvider
{
    private readonly Dictionary<string, byte[]> _resources = new(StringComparer.Ordinal);

    public string Framework => "html";

    public StaticHtmlUiProvider Add(string resourceUri, string html)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceUri);
        ArgumentNullException.ThrowIfNull(html);
        if (!Uri.TryCreate(resourceUri, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "ui", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Resource URI must be an absolute ui:// URI.", nameof(resourceUri));
        _resources.Add(resourceUri, Encoding.UTF8.GetBytes(html));
        return this;
    }

    public ValueTask<UnisonoUiAsset> GetAssetAsync(
        UnisonoUiDefinition definition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_resources.TryGetValue(definition.ResourceUri, out var bytes))
            throw new KeyNotFoundException($"No HTML resource registered for '{definition.ResourceUri}'.");

        return ValueTask.FromResult(new UnisonoUiAsset(
            definition.ResourceUri, "text/html;profile=mcp-app", bytes));
    }
}
