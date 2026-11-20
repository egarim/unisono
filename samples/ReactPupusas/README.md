# React Pupusas — third pluggable UI provider 🫓

React is **optional** in Unisono: a React UI is built by Vite, while the .NET server only consumes the **published build output** using `Unisono.UI.React.ReactUiProvider`. The core tool definition remains the same regardless of UI framework.

## Step 1. Run it locally

Requirements: Node.js 20.19+ or 22.12+, npm.

```bash
cd samples/ReactPupusas
npm install
npm run dev
```

Open the Vite local URL to try the Pupusa Party calculator. This frontend calculates the total **locally in React**, not by invoking an MCP tool.

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

## Step 4. Understand the remaining integration work

This PR adds the **React build-output adapter**, not a working embedded ChatGPT/Claude widget. The MCP host still needs to advertise tool `_meta.ui.resourceUri`, implement MCP `resources/read` for `ui://` resources, declare CSP asset domains, and add the browser-side MCP Apps protocol bridge. Only then can the React UI invoke the C# tool through the host.

## One application, three UI options

| Provider | Frontend input | Published build | .NET package |
| --- | --- | --- | --- |
| React | TypeScript + JSX | Vite `dist/` | `Unisono.UI.React` |
| Blazor | Razor + C# | WASM `wwwroot/` | `Unisono.UI.Blazor` |
| Uno | XAML + C# | WASM `wwwroot/` | `Unisono.UI.Uno` |

All three implement the same `IUnisonoUiProvider` contract. The current registry maps **one provider per tool**; to switch dynamically by host, extend the registry rather than registering duplicate resource URIs.

The React sample has not been npm-built or tested in ChatGPT/Claude in this environment.
