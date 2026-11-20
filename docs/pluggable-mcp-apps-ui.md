# Pluggable MCP Apps UI (initial foundation)

`Unisono.McpApps` defines a **UI-framework-neutral** registry and provider interface. It does not depend on Uno, Blazor, React, or a browser runtime.

## Current scope

- Link existing `ToolRegistry` tool names to `ui://` resources.
- Register arbitrary `IUnisonoUiProvider` implementations.
- Resolve compiled assets from a provider and validate their URI and MIME type.
- Check host capabilities and fall back to headless tool invocation when UI is unsupported.
- Use `StaticHtmlUiProvider` as a reference implementation for prebuilt HTML.

This milestone **does not yet wire MCP resources/read, tools/list UI metadata, the browser host bridge, or marketplace packaging**. A provider alone does not make the resource render in ChatGPT or Claude.

## Example

```csharp
using Unisono;
using Unisono.McpApps;

var tools = new ToolRegistry().AddToolsFrom(new OrderTools());
var html = new StaticHtmlUiProvider()
    .Add("ui://orders/create", "<!doctype html><html><body><h1>Create order</h1></body></html>");

var ui = new UnisonoUiRegistry(tools)
    .AddProvider(html)
    .MapTool("create_order", "ui://orders/create", "html", "Create order");

if (ui.CanRender("create_order", new UnisonoUiHostCapabilities(SupportsMcpApps: true)))
{
    var resource = await ui.GetAssetAsync("create_order");
    // The future MCP Apps transport adapter will serve this as a ui:// resource.
}
```

The `create_order` tool must exist in `ToolRegistry`, using its MCP tool name, not necessarily its C# method name.

## Adding an Uno or Blazor provider

Implement `IUnisonoUiProvider` in a separate package (`Unisono.UI.Uno` or `Unisono.UI.Blazor`). Build the chosen UI to web assets and return a `UnisonoUiAsset` with the mapped URI and MCP Apps HTML MIME type. Registration is the same for every UI framework:

```csharp
ui.AddProvider(myUnoProvider)
  .MapTool("create_order", "ui://orders/create", "uno", "Create order");
```

The adapter should handle framework-specific compilation and asset loading. The generic registry must not reference Uno or Blazor types.

## Next implementation milestones

1. Add MCP protocol exposure of `ui://` resources and `_meta.ui.resourceUri` for mapped tools.
2. Implement the MCP Apps host/client JavaScript bridge and verify actual host sandbox behavior.
3. Build `Unisono.UI.Uno` and `Unisono.UI.Blazor` adapters and compatibility tests.
4. Generate project templates and OpenAI/Claude distribution artifacts separately from UI compilation.
5. Add automated tests for missing providers, duplicate resources, unknown tools, MIME verification, and no-UI fallback.

**Security:** treat UI content as active, untrusted-from-the-host's-perspective web code. UI execution must not bypass existing authorization on tool calls. Avoid exposing app credentials to the browser iframe.
