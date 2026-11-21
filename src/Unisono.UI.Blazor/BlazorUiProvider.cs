using Unisono.McpApps;

namespace Unisono.UI.Blazor;

/// <summary>
/// Loads the published wwwroot/index.html of a standalone Blazor WASM app.
/// Deploy the entire wwwroot folder to AssetBaseUri before publishing MCP Apps.
/// </summary>
public sealed class BlazorUiProvider : PublishedWebUiProvider
{
    public BlazorUiProvider(string publishedWwwroot, Uri assetBaseUri)
        : base(publishedWwwroot, assetBaseUri) { }

    public override string Framework => "blazor";

    public new BlazorUiProvider Add(string resourceUri, string entrypoint = "index.html")
    {
        base.Add(resourceUri, entrypoint);
        return this;
    }
}
