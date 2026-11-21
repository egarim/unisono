using Unisono.McpApps;

namespace Unisono.UI.Uno;

/// <summary>
/// Loads an Uno Platform WebAssembly published index document.
/// The matching publish/wwwroot assets must be deployed to AssetBaseUri.
/// </summary>
public sealed class UnoUiProvider : PublishedWebUiProvider
{
    public UnoUiProvider(string publishedWwwroot, Uri assetBaseUri)
        : base(publishedWwwroot, assetBaseUri) { }

    public override string Framework => "uno";

    public new UnoUiProvider Add(string resourceUri, string entrypoint = "index.html")
    {
        base.Add(resourceUri, entrypoint);
        return this;
    }
}
