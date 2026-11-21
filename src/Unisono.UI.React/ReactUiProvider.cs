using Unisono.McpApps;

namespace Unisono.UI.React;

/// <summary>
/// Exposes a prebuilt React application (such as a Vite dist folder) as a UI resource.
/// Build React with Vite's base option matching the asset base URI passed to the constructor
/// and publish the full dist folder at that URL before loading the MCP App.
/// </summary>
/// <remarks>
/// This is a published-assets adapter, not a JavaScript runtime or MCP Apps browser bridge.
/// No React or Node.js dependency is required by the .NET server.
/// </remarks>
public sealed class ReactUiProvider : PublishedWebUiProvider
{
    public ReactUiProvider(string publishedDistDirectory)
        : base(publishedDistDirectory, null) { }

    public ReactUiProvider(string publishedDistDirectory, Uri assetBaseUri)
        : base(publishedDistDirectory, assetBaseUri) { }

    public override string Framework => "react";

    public new ReactUiProvider Add(string resourceUri, string entrypoint = "index.html")
    {
        base.Add(resourceUri, entrypoint);
        return this;
    }
}
