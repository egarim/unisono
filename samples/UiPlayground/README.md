# UI Playground — a pupusa-sized introduction 🫓

**One C# tool. One optional HTML card. No React, Uno, or Blazor required.**

This is a runnable demonstration of the new `Unisono.McpApps` provider abstraction. It teaches *registration and resource resolution*, **not** a fully working embedded ChatGPT/Claude UI.

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

## 4. Understand the missing bridge

Currently, `Unisono.McpApps` manages UI providers and resources in .NET, but `UnisonoMcpHttpHost` **does not yet publish** that resource through MCP's `resources/read` protocol or connect `_meta.ui.resourceUri` to a tool. There is no MCP Apps iframe bridge yet. That is why opening this sample in ChatGPT or Claude **will expose the tool, not the embedded card**.

Next, we will add protocol exposure, verify host compatibility, and provide a real Uno Platform WebAssembly UI provider.

More detail: [pluggable MCP Apps architecture](../../docs/pluggable-mcp-apps-ui.md).
