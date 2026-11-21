using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Unisono.McpApps;

/// <summary>
/// Base provider for independently published WebAssembly applications.
/// The index document is delivered as a UI resource, while the WASM/JS/CSS
/// dependencies are downloaded from an explicitly configured HTTPS origin.
/// The eventual MCP resource adapter must allow that origin in resourceDomains.
/// </summary>
public abstract class PublishedWebUiProvider : IUnisonoUiProvider
{
    private readonly string _publishRoot;
    private readonly Uri? _assetBaseUri;
    private readonly Dictionary<string, string> _pages = new(StringComparer.Ordinal);

    protected PublishedWebUiProvider(string publishRoot, Uri? assetBaseUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publishRoot);
        if (assetBaseUri is not null && (!assetBaseUri.IsAbsoluteUri ||
            (assetBaseUri.Scheme != Uri.UriSchemeHttps &&
             !(assetBaseUri.Scheme == Uri.UriSchemeHttp && assetBaseUri.IsLoopback))))
            throw new ArgumentException("Asset base URI must be HTTPS (HTTP is allowed for loopback development only).",
                nameof(assetBaseUri));
        if (assetBaseUri is not null && (!string.IsNullOrEmpty(assetBaseUri.Query) || !string.IsNullOrEmpty(assetBaseUri.Fragment)))
            throw new ArgumentException("Asset base URI cannot contain a query or fragment.", nameof(assetBaseUri));

        _publishRoot = Path.GetFullPath(publishRoot);
        _assetBaseUri = assetBaseUri is null ? null : new Uri(assetBaseUri.AbsoluteUri.TrimEnd('/') + "/");
    }

    public abstract string Framework { get; }

    /// <summary>The origin that the MCP Apps host must allow for static resources.</summary>
    public Uri? AssetBaseUri => _assetBaseUri;

    /// <summary>
    /// Associates a UI resource with a relative HTML entrypoint (usually index.html).
    /// Entrypoint must remain inside the publish directory.
    /// </summary>
    public PublishedWebUiProvider Add(string resourceUri, string entrypoint = "index.html")
    {
        if (!Uri.TryCreate(resourceUri, UriKind.Absolute, out var uri) || uri.Scheme != "ui")
            throw new ArgumentException("Expected a ui:// resource URI.", nameof(resourceUri));
        ArgumentException.ThrowIfNullOrWhiteSpace(entrypoint);
        if (Path.IsPathRooted(entrypoint) || entrypoint.Split('/', '\\').Any(s => s == ".."))
            throw new ArgumentException("Entrypoint must be a relative path without traversal.", nameof(entrypoint));
        _pages.Add(resourceUri, entrypoint);
        return this;
    }

    public async ValueTask<UnisonoUiAsset> GetAssetAsync(
        UnisonoUiDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!string.Equals(definition.Framework, Framework, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("UI resource belongs to another framework.", nameof(definition));
        if (!_pages.TryGetValue(definition.ResourceUri, out var entrypoint))
            throw new KeyNotFoundException($"No UI page registered for {definition.ResourceUri}.");

        var fullPath = Path.GetFullPath(Path.Combine(_publishRoot, entrypoint));
        if (!fullPath.StartsWith(_publishRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(fullPath, _publishRoot, StringComparison.Ordinal))
            throw new InvalidOperationException("UI entrypoint escapes the publish directory.");
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Publish the framework UI and configure its wwwroot path.", fullPath);

        var html = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        if (!Regex.IsMatch(html, @"<head\b", RegexOptions.IgnoreCase))
            throw new InvalidDataException("The published page needs a <head> element.");

        // A sandboxed MCP App is not hosted at the static site's URL. Supply
        // an absolute base so the framework's boot resources resolve correctly.
        if (_assetBaseUri is not null)
        {
        var baseTag = "<base href=\"" + WebUtility.HtmlEncode(_assetBaseUri.AbsoluteUri) + "\">";
        html = Regex.IsMatch(html, @"<base\s", RegexOptions.IgnoreCase)
            ? Regex.Replace(html, @"<base\b[^>]*>", _ => baseTag, RegexOptions.IgnoreCase)
            : Regex.Replace(html, @"<head\b[^>]*>", m => m.Value + baseTag,
                RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        }

        return new UnisonoUiAsset(
            definition.ResourceUri,
            "text/html;profile=mcp-app",
            Encoding.UTF8.GetBytes(html));
    }
}
