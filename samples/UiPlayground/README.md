# UI Playground — a pupusa-sized introduction 🫓

**One C# tool. One optional HTML card. No React, Uno, or Blazor required.**

This is a runnable demonstration of the `Unisono.McpApps` provider abstraction. It teaches *registration and resource resolution* and a local preview. To serve the same resource to an MCP host, see [the protocol host sample](../McpAppsPupusasHost/README.md).

## 1. Start with the tool

`PupusaTools.CalculatePupusas` is a regular C# method with `[McpServerTool]`. Its MCP tool name is `calculate_pupusas`. It works in headless MCP clients whether UI support exists or not.

## 2. Hand Unisono a UI

The sample loads `order-card.html` into `StaticHtmlUiProvider`, then links the UI resource to `calculate_pupusas`:

```csharp
var ui = new UnisonoUiRegistry(tools)
    .AddProvider(new StaticHtmlUiProvider().Add("ui://pupuseria/order-card", html))
    .MapTool("calculate_pupusas", "ui://pupuseria/order-card", "html");
```

`MapTool` checks that the tool and provider exist. You can replace `html` with a future Uno or Blazor provider, without changing the C# tool.

## 3. Run it

Requires the .NET 10 SDK. From the repository root:

```bash
dotnet run --project samples/UiPlayground
# or render the same tool from a generated JSON document instead of the HTML card:
dotnet run --project samples/UiPlayground -- --catalog
```

Expected console lines (paths vary):

```text
Three pupusas cost $5.25.
UI mapping: calculate_pupusas → ui://pupuseria/order-card (text/html;profile=mcp-app)
Browser-only preview: .../pupusa-preview.html
Headless MCP tools available at http://127.0.0.1:8791/mcp
```

Open `pupusa-preview.html` in a web browser to play with the price calculator. The browser demo uses local JavaScript, **not MCP tool calls**. To test the real C# tool over MCP, run this in another terminal:

```bash
scripts/mcp-smoke.sh http://127.0.0.1:8791/mcp calculate_pupusas '{"quantity":3,"priceEach":1.75}'
```

## 4. From preview to a real MCP App

This playground stops at the local preview. The protocol part is now implemented: when a host is created with the
registry, `UnisonoMcpHttpHost` publishes the mapped `ui://` resource through `resources/list` and `resources/read`,
advertises `_meta.ui.resourceUri` on the tool and declares an asset origin in `_meta.ui.csp.resourceDomains`.
`samples/McpAppsPupusasHost` does exactly that, with React, Blazor or Uno UIs, and `scripts/mcp-ui-smoke.py`
verifies it.

Adding it here is one argument (`--catalog` does exactly that):

```csharp
await using var host = new UnisonoMcpHttpHost(tools, options, ui: ui);
```

The `--catalog` run also prints the generated document and writes the rendered preview. It uses
[`Unisono.UI.Catalog`](../../docs/declarative-ui.md) instead of `order-card.html`.

Opening the HTML file in a browser still does not involve an MCP host; the card only becomes interactive inside an
MCP Apps-compatible host. Real ChatGPT/Claude sandbox behavior remains unverified.

More detail: [pluggable MCP Apps architecture](../../docs/pluggable-mcp-apps-ui.md).
