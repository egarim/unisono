# React Pupusas — third pluggable UI provider 🫓

React is **optional** in Unisono: a React UI is built by Vite, while the .NET server only consumes the **published build output** using `Unisono.UI.React.ReactUiProvider`. The core tool definition remains the same regardless of UI framework.

## Step 1. Run it locally

Requirements: Node.js 20.19+ or 22.12+, npm.

```bash
cd samples/ReactPupusas
npm install
npm run dev
```

The component now calls `app.callServerTool({ name: 'calculate_pupusas', arguments: ... })` via the official MCP Apps SDK. A plain Vite tab does **not** provide an MCP Apps host, so the button remains unavailable without a compatible host. Use the [real MCP server sample](../McpAppsPupusasHost/README.md) for integration testing.

## Step 2. Build for the static asset host

A future ChatGPT/Claude MCP Apps host receives the `index.html` via an MCP `ui://` resource. Its compiled JS and CSS must still load from an allowed static asset origin, so generate **absolute asset URLs** at build time:

```bash
UNISONO_UI_BASE=https://static.example.com/react-pupusas/ npm run build
```

This produces `samples/ReactPupusas/dist/index.html` and `dist/assets/*`. Deploy the **whole dist directory contents** to the matching HTTPS URL. Don't serve just the HTML. Keep Vite's build base in sync with the public URL; a `<base>` tag alone does not rewrite Vite's absolute script URLs. Configure the host's resource CSP to allow this asset origin.

## Step 3. Link the React build to a C# tool

Reference `src/Unisono.UI.React/Unisono.UI.React.csproj` (until this package is published). In your .NET host:

```csharp
using Unisono;
using Unisono.McpApps;
using Unisono.UI.React;

var tools = new ToolRegistry().AddToolsFrom(new PupusaTools());
var provider = new ReactUiProvider(
    "samples/ReactPupusas/dist",
    new Uri("https://static.example.com/react-pupusas/"))
    .Add("ui://pupuseria/react");

var ui = new UnisonoUiRegistry(tools)
    .AddProvider(provider)
    .MapTool("calculate_pupusas", "ui://pupuseria/react", "react");

var asset = await ui.GetAssetAsync("calculate_pupusas");
// asset.MimeType == "text/html;profile=mcp-app"
// asset.Content contains Vite's HTML entrypoint with correct asset URLs.
```

`PupusaTools` is the same tool class shown in `samples/UiPlayground/` (on the separate README-example PR). You must register a tool named `calculate_pupusas` before mapping its UI.

## Step 4. Test MCP Apps integration

The MCP host now publishes a mapped `ui://` resource, advertises tool `_meta.ui.resourceUri`, and declares hosted asset resourceDomains. The React component connects through the official `useApp` hook and invokes the real C# tool. Run [McpAppsPupusasHost](../McpAppsPupusasHost/README.md) and validate with `scripts/mcp-ui-smoke.py`. Actual ChatGPT and Claude rendering **still needs live verification**. Opening a regular website is not a substitute for a host.

## One application, three UI options

| Provider | Frontend input | Published build | .NET package |
| --- | --- | --- | --- |
| React | TypeScript + JSX | Vite `dist/` | `Unisono.UI.React` |
| Blazor | Razor + C# | WASM `wwwroot/` | `Unisono.UI.Blazor` |
| Uno | XAML + C# | WASM `wwwroot/` | `Unisono.UI.Uno` |

All three implement the same `IUnisonoUiProvider` contract. The current registry maps **one provider per tool**; to switch dynamically by host, extend the registry rather than registering duplicate resource URIs.

The React sample has not been npm-built or tested inside ChatGPT/Claude in this environment. Build CI and real-host testing are required.
