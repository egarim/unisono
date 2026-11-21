# Pluggable MCP Apps UI

`Unisono.McpApps` defines a **UI-framework-neutral** registry and provider interface. It does not depend on Uno, Blazor, React, or a browser runtime.

## Current scope

- Link existing `ToolRegistry` tool names to `ui://` resources.
- Register arbitrary `IUnisonoUiProvider` implementations.
- Resolve compiled assets from a provider and validate their URI and MIME type.
- Check host capabilities and fall back to headless tool invocation when UI is unsupported.
- Use `StaticHtmlUiProvider` as a reference implementation for prebuilt HTML.
- Publish mapped `ui://` resources through MCP `resources/list` and `resources/read`, and advertise
  `_meta.ui.resourceUri` on the matching tools when the host is created with a UI registry.
- Declare the asset origin in `_meta.ui.csp.resourceDomains` for published WASM/Vite providers.
- Ship Uno, Blazor and React adapters on the same `IUnisonoUiProvider` contract
  (`Unisono.UI.Uno`, `Unisono.UI.Blazor`, `Unisono.UI.React`), plus a shared browser bridge for
  .NET WebAssembly frontends.

A provider alone does not make the resource render in ChatGPT or Claude: the browser iframe,
sandbox CSP and host/client capability negotiation still have to be verified against each real host.

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
    // UnisonoMcpHttpHost(..., ui: ui) serves this as a ui:// resource over MCP.
}
```

The `create_order` tool must exist in `ToolRegistry`, using its MCP tool name, not necessarily its C# method name.

## Adding an Uno or Blazor provider

The `Unisono.UI.Uno`, `Unisono.UI.Blazor` and `Unisono.UI.React` packages implement `IUnisonoUiProvider` by wrapping
an already-published `wwwroot`/`dist` folder. A new framework follows the same shape: implement
`IUnisonoUiProvider` in its own package, build the UI to web assets, and return a `UnisonoUiAsset` with the mapped
URI and MCP Apps HTML MIME type. Registration is the same for every UI framework:

```csharp
ui.AddProvider(myUnoProvider)
  .MapTool("create_order", "ui://orders/create", "uno", "Create order");
```

The adapter should handle framework-specific compilation and asset loading. The generic registry must not reference Uno or Blazor types.

## Next implementation milestones

1. ~~Add MCP protocol exposure of `ui://` resources and `_meta.ui.resourceUri` for mapped tools.~~ Done:
   `UnisonoMcpAppsResources` and the `ui:` parameter of `UnisonoMcpHttpHost`.
2. ~~Implement the MCP Apps host/client JavaScript bridge~~ Done for React (official SDK), Blazor and Uno
   (shared `samples/McpAppsBridge`). Still open: verify actual host sandbox behavior in ChatGPT and Claude.
3. ~~Build `Unisono.UI.Uno` and `Unisono.UI.Blazor` adapters~~ Done as published-asset providers; a verified
   Uno WebAssembly build and compatibility tests are still open.
4. Generate project templates and OpenAI/Claude distribution artifacts separately from UI compilation.
5. Add automated tests for missing providers, duplicate resources, unknown tools, MIME verification, and no-UI fallback.

**Security:** treat UI content as active, untrusted-from-the-host's-perspective web code. UI execution must not bypass existing authorization on tool calls. Avoid exposing app credentials to the browser iframe.
