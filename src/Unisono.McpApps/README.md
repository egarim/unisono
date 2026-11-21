# Unisono.McpApps

Optional [MCP Apps](https://github.com/modelcontextprotocol/ext-apps) layer for [Unisono](https://github.com/egarim/unisono):
associate a C# tool with a `ui://` resource without picking a UI toolkit in `Unisono.Core`.

`Unisono.McpApps` holds the framework-neutral contract (`IUnisonoUiProvider`, `UnisonoUiRegistry`) and the adapter
that hands mapped resources and tool metadata to the official MCP C# SDK. React, Blazor and Uno providers live in
their own optional packages; `StaticHtmlUiProvider` is built in.

```bash
dotnet add package Unisono.McpApps --prerelease
```

```csharp
var tools = new ToolRegistry().AddToolsFrom(new OrderTools());

var ui = new UnisonoUiRegistry(tools)
    .AddProvider(new StaticHtmlUiProvider().Add("ui://orders/create", trustedHtml))
    .MapTool("create_order", "ui://orders/create", "html", "Create order");

// Pass the registry to the host; without it the host serves tools exactly as before.
await using var host = new UnisonoMcpHttpHost(
    tools, new UnisonoMcpHttpOptions { Port = 8787 }, ui: ui);
host.Start();
```

When a UI registry is supplied, the host

- lists each mapped `ui://` resource in `resources/list` and serves it from `resources/read` as
  `text/html;profile=mcp-app`,
- adds `_meta.ui.resourceUri` to the matching tool in `tools/list`,
- declares the asset origin in `_meta.ui.csp.resourceDomains` for providers that load published WebAssembly/Vite
  assets,
- leaves plain `tools/call` untouched, so clients without UI support keep working.

The mapping is written onto the registry's shared MCP tool objects, so create one host per `ToolRegistry`. Content is
trusted UI supplied by the app developer; tool authorization stays on the server.

Published-asset providers (React `dist`, Blazor/Uno `wwwroot`) require HTTPS (loopback HTTP is allowed for
development) and rewrite the page's `<base href>` so framework assets load from the asset origin.

Real ChatGPT/Claude iframe and CSP behavior is not verified yet. See
[pluggable MCP Apps UI](https://github.com/egarim/unisono/blob/main/docs/pluggable-mcp-apps-ui.md) and the
[sample host](https://github.com/egarim/unisono/tree/main/samples/McpAppsPupusasHost).
