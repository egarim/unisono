using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Unisono.McpApps;

/// <summary>
/// Connects pluggable UI assets to the official MCP SDK resource and tool primitives.
/// A non-UI MCP client can continue invoking exactly the same tools.
/// </summary>
public static class UnisonoMcpAppsResources
{
    public static McpServerResourceCollection CreateResources(UnisonoUiRegistry ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        var resources = new McpServerResourceCollection();

        foreach (var definition in ui.Definitions)
        {
            var current = definition;
            var meta = BuildResourceMeta(ui, current);

            resources.Add(McpServerResource.Create(
                (Func<CancellationToken, Task<TextResourceContents>>)(async ct =>
                {
                    var asset = await ui.GetAssetAsync(current.ToolName, ct).ConfigureAwait(false);
                    return new TextResourceContents
                    {
                        Uri = current.ResourceUri,
                        MimeType = asset.MimeType,
                        Text = Encoding.UTF8.GetString(asset.Content.Span),
                        Meta = (JsonObject)meta.DeepClone()
                    };
                }),
                new McpServerResourceCreateOptions
                {
                    UriTemplate = current.ResourceUri,
                    Name = current.Title ?? current.ToolName,
                    MimeType = "text/html;profile=mcp-app",
                    Meta = (JsonObject)meta.DeepClone()
                }));
        }

        return resources;
    }

    public static void ApplyToolMetadata(
        UnisonoUiRegistry ui,
        IEnumerable<McpServerTool> tools)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(tools);

        foreach (var tool in tools)
        {
            if (!ui.TryGetDefinition(tool.ProtocolTool.Name, out var definition) || definition is null)
                continue;

            // Preserve metadata from other protocol extensions.
            var meta = tool.ProtocolTool.Meta ?? new JsonObject();
            var uiMeta = meta["ui"] as JsonObject ?? new JsonObject();
            uiMeta["resourceUri"] = definition.ResourceUri;
            meta["ui"] = uiMeta;
            tool.ProtocolTool.Meta = meta;
        }
    }

    private static JsonObject BuildResourceMeta(
        UnisonoUiRegistry ui, UnisonoUiDefinition definition)
    {
        var uiMeta = new JsonObject();
        if (ui.GetProvider(definition.Framework) is PublishedWebUiProvider { AssetBaseUri: not null } hosted)
        {
            // MCP Apps CSP uses origins; do not include a path in resourceDomains.
            var origin = hosted.AssetBaseUri.GetLeftPart(UriPartial.Authority);
            uiMeta["csp"] = new JsonObject
            {
                ["resourceDomains"] = new JsonArray(origin)
            };
        }

        return new JsonObject { ["ui"] = uiMeta };
    }
}
