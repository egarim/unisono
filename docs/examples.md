# Unisono examples: one tool, many front doors 🎼

Welcome to the Unisono tasting menu. Start with **plain C#**, add an optional interface, then choose your favorite frontend. Nothing in your tool needs to know whether its caller is Siri, Claude Code, an MCP Apps iframe, or a regular .NET process.

> **Current state:** the examples below reflect code on this development branch, not necessarily released NuGet packages. The React, Blazor and Uno MCP Apps examples are experimental and **have not been verified inside real ChatGPT/Claude hosts**. The repository includes a CI workflow and an MCP protocol smoke test; check their results before production use.

All shell commands are run from the **repository root** unless a `cd` says otherwise. You'll need the **.NET 10 SDK**; React and the shared browser bridge also need Node.js/npm. All example host URLs use a local development endpoint; a public connector needs an authenticated HTTPS deployment.

## 1. The smallest possible MCP tool — no UI at all 🧮

**Goal:** expose `add` and `echo` so an assistant can use them. No web framework and no Node.js.

```bash
dotnet run --project samples/ConsoleHost -- 8788
```

In another terminal, from the repository root:

```bash
scripts/mcp-smoke.sh http://127.0.0.1:8788/mcp add '{"a":2,"b":3}'
```

The example is defined in [`samples/ConsoleHost/Program.cs`](../samples/ConsoleHost/Program.cs). A tool is just a C# method:

```csharp
[McpServerTool(Name = "add", ReadOnly = true, Destructive = false)]
[Description("Adds two numbers.")]
public double Add(double a, double b) => a + b;
```

**What to notice:** the method is registered once with `ToolRegistry.AddToolsFrom`; MCP clients can call it without knowing C# or how the application was built.

## 2. A little HTML personality — without a frontend framework 🫓

**Goal:** attach a small card to the C# `calculate_pupusas` tool, but let headless agents continue using it without a screen.

```bash
dotnet run --project samples/UiPlayground
```

The sample creates `pupusa-preview.html` in your current directory. Open it in a browser. The **preview computes the amount locally in JavaScript**; it is not a remote tool call. To call the C# tool, use a second terminal:

```bash
scripts/mcp-smoke.sh http://127.0.0.1:8791/mcp calculate_pupusas '{"quantity":3,"priceEach":1.75}'
```

The central registration is:

```csharp
var tools = new ToolRegistry().AddToolsFrom(new PupusaTools());
var html = new StaticHtmlUiProvider()
    .Add("ui://pupuseria/order-card", htmlSource);

var ui = new UnisonoUiRegistry(tools)
    .AddProvider(html)
    .MapTool("calculate_pupusas", "ui://pupuseria/order-card", "html");
```

Here, `htmlSource` means your own trusted HTML string, while `PupusaTools` is the class implementing the method. The full source is in [`samples/UiPlayground`](../samples/UiPlayground/README.md).

**What to notice:** the `ui://` resource is mapped to a tool name, not to a particular language or UI toolkit. The code remains useful even when an assistant has no UI rendering support.

## 3. React + TypeScript: actually call C# from an MCP App ⚛️

**Goal:** give the pupusa calculator a React form and make its **Calculate** button invoke the **real C# MCP tool** through a compatible MCP Apps host.

First, build the self-contained frontend:

```bash
cd samples/ReactPupusas
npm install
npm run build
cd ../..
```

Vite bundles the UI and its JavaScript into `samples/ReactPupusas/dist/index.html`. Next, start the C# host:

```bash
dotnet run --project samples/McpAppsPupusasHost -- react samples/ReactPupusas/dist
```

In a separate terminal, check what the MCP server actually exposes:

```bash
python3 scripts/mcp-ui-smoke.py http://127.0.0.1:8792/mcp
```

The [React component](../samples/ReactPupusas/src/PupusaCard.tsx) calls:

```typescript
const response = await app.callServerTool({
  name: "calculate_pupusas",
  arguments: { quantity, priceEach },
});
```

The server binds the same tool to `ui://pupuseria/order`:

```csharp
var provider = new ReactUiProvider("samples/ReactPupusas/dist")
    .Add("ui://pupuseria/order");

var ui = new UnisonoUiRegistry(tools)
    .AddProvider(provider)
    .MapTool("calculate_pupusas", "ui://pupuseria/order", "react");
```

**What to notice:** `tools/list` advertises the UI resource, `resources/read` returns the HTML, and the embedded application calls `tools/call`. The protocol smoke test checks the first two plus the headless call. **It does not prove a live ChatGPT/Claude iframe works.** Opening the HTML in a normal browser cannot establish the MCP Apps host bridge.

## 4. Blazor WebAssembly: Razor and C#, no React required 🟣

**Goal:** use an interactive Razor component while keeping the same C# MCP tool and server.

Build the shared browser-to-MCP bridge:

```bash
cd samples/McpAppsBridge
npm install
npm run build
cp dist/unisono-mcp-bridge.js ../BlazorPupusas/wwwroot/
cd ../..
```

Publish Blazor:

```bash
dotnet publish samples/BlazorPupusas/BlazorPupusas.csproj -c Release -o out/blazor
```

Deploy the entire `out/blazor/wwwroot` tree to a static HTTPS URL, for example `https://static.example.com/blazor-pupusas/`. The URL in this guide is an **example placeholder**, not an existing deployment. Then run:

```bash
dotnet run --project samples/McpAppsPupusasHost -- blazor out/blazor/wwwroot https://static.example.com/blazor-pupusas/
```

The [Razor component](../samples/BlazorPupusas/PupusaCard.razor) invokes the bridge via `IJSRuntime`:

```csharp
Total = await JS.InvokeAsync<decimal>(
    "UnisonoMcpBridge.calculatePupusas", quantity, priceEach);
```

**What to notice:** the bridge is shared with Uno, but the UI itself stays in Razor/C#. Blazor requires publicly reachable WASM, script and stylesheet assets; a compatible host must allow those resources through its sandbox and CSP.

## 5. Uno Platform: C# + XAML, same tool again 🪟

**Goal:** reuse the tool from an Uno WebAssembly interface rather than building a React or Blazor screen.

This sample is a **drop-in page**, not a complete Uno project. Use the current official Uno project template to create a browser/WebAssembly app named `UnoPupusas`, then copy:

- [`MainPage.xaml`](../samples/UnoPupusas/MainPage.xaml)
- [`MainPage.xaml.cs`](../samples/UnoPupusas/MainPage.xaml.cs)
- [`BrowserMcpBridge.cs`](../samples/UnoPupusas/BrowserMcpBridge.cs)

Build the shared browser bridge as in the Blazor example. Add `unisono-mcp-bridge.js` to your Uno web assets and load it **before** the Uno WASM startup script. Publish using the framework and target of your generated Uno project; for typical `net10.0-browserwasm` projects:

```bash
dotnet publish -f net10.0-browserwasm -c Release -o publish
```

Deploy its published web assets to HTTPS, then from the Unisono repository root use:

```bash
dotnet run --project samples/McpAppsPupusasHost -- uno /path/to/uno/publish/wwwroot https://static.example.com/uno-pupusas/
```

The page's button uses a browser WASM import which calls `UnisonoMcpBridge.calculatePupusas`, passing the result back to XAML:

```csharp
var total = await BrowserMcpBridge.CalculatePupusasAsync(
    (int)quantity, price);
```

**What to notice:** `Unisono.Core` doesn't care that the UI is rendered through Uno. **This path is particularly experimental:** SDK-specific Uno bootstrap and `JSImport` behavior need compilation and browser verification.

## 6. Native mobile, not a chat widget: the original Unisono 🎤

**Goal:** expose tasks to Siri/Shortcuts and MCP from the same MAUI app.

The [TaskTracker sample](../samples/TaskTracker/) predates these UI adapters. Its existing `TaskTools` handler is used by the generated Apple App Intents and by an MCP endpoint. Build on macOS with the appropriate Xcode and .NET workloads:

```bash
dotnet build samples/TaskTracker/MauiAppIntentsSample/MauiAppIntentsSample.csproj \
  -f net10.0-ios -r iossimulator-arm64 \
  -p:CodesignEntitlements="" -p:ValidateXcodeVersion=false
```

With the sample running in the iOS simulator, the development host uses port 8787:

```bash
scripts/mcp-smoke.sh http://127.0.0.1:8787/mcp create_task '{"title":"Buy masa","priority":"high"}'
```

**What to notice:** this is a native assistant integration, **not** an embedded MCP App iframe. Both are valid Unisono front doors, and neither should depend on the other.

## Which example should you start with?

| I want to… | Start here | UI runtime |
| --- | --- | --- |
| Expose a C# tool quickly | ConsoleHost | None |
| Understand UI mapping | UiPlayground | Plain HTML |
| Prototype an embedded AI interface | ReactPupusas + McpAppsPupusasHost | React |
| Keep my interface in .NET/Razor | BlazorPupusas | Blazor WASM |
| Use WinUI-like XAML | UnoPupusas | Uno WASM |
| Build Siri/Shortcuts actions | TaskTracker | Native iOS |

**One rule throughout:** tool authorization and validation belong on the server. A beautiful card is not a security boundary.

## What remains before public distribution?

The examples demonstrate the Unisono tool registry, optional UI providers, and intended MCP Apps protocol integration. **They are not automatically marketplace-ready packages.** Before ChatGPT or Claude public distribution, validate builds and runtime compatibility; expose the server over authenticated HTTPS; enforce per-user permissions; test host-specific iframe sandbox/CSP and OAuth behavior; and follow each provider's marketplace submission process.

For implementation details see [the architecture](pluggable-mcp-apps-ui.md), [the framework provider guide](uno-blazor-ui-providers.md), and [the MCP Apps server sample](../samples/McpAppsPupusasHost/README.md).
