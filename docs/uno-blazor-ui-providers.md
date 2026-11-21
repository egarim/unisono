# Plug in Uno, Blazor, or React 🫓

> For forms, lists and tables you may not need any of these: [`Unisono.UI.Catalog`](declarative-ui.md)
> generates a declarative UI with no frontend project. The providers here are for tools that need a real UI.

**Write your UI in C# + XAML (Uno) or C# + Razor (Blazor). Keep the same Unisono tool.**

This feature introduces three providers:

- `Unisono.UI.Uno.UnoUiProvider`
- `Unisono.UI.Blazor.BlazorUiProvider`
- `Unisono.UI.React.ReactUiProvider`

All three implement `IUnisonoUiProvider` through `PublishedWebUiProvider`. They are **publish-output adapters**: Uno and Blazor load a published WebAssembly `wwwroot/index.html`, while React loads Vite's `dist/index.html`. The JS, CSS, WASM, and other assets must be hosted at a trusted HTTPS origin.

**Integration (experimental):** `UnisonoMcpHttpHost` now exposes mapped resources through MCP, adds tool `_meta.ui.resourceUri`, and emits resource CSP metadata for hosted assets. React has an MCP Apps SDK tool call; Uno and Blazor samples invoke the shared browser bridge. Build/test validation and real-host sandbox compatibility still need verification. Authentication, HTTPS deployment, and publishing remain explicit deployment steps.

## Blazor: start with a complete sample

```bash
dotnet publish samples/BlazorPupusas/BlazorPupusas.csproj -c Release -o ./out/blazor
```

Deploy **all** files from `out/blazor/wwwroot/` to `https://static.example.com/blazor-pupusas/`. Merely setting an asset URL in C# does not upload files.

```csharp
using Unisono;
using Unisono.McpApps;
using Unisono.UI.Blazor;

var tools = new ToolRegistry().AddToolsFrom(new PupusaTools());
var provider = new BlazorUiProvider(
    "out/blazor/wwwroot",
    new Uri("https://static.example.com/blazor-pupusas/"))
    .Add("ui://pupuseria/blazor");

var ui = new UnisonoUiRegistry(tools)
    .AddProvider(provider)
    .MapTool("calculate_pupusas", "ui://pupuseria/blazor", "blazor");

var resource = await ui.GetAssetAsync("calculate_pupusas");
// The MCP host exposes this via resources/read when created with ui: ui.
```

`PupusaTools` is the tool class in the existing sample. Use the actual C# tool's MCP name, not its display name.

## Uno Platform: use the current official app scaffold

Uno versions bring their own WASM bootstrap and SDK configuration, so the sample intentionally supplies a **drop-in XAML page and code-behind** at `samples/UnoPupusas/`, rather than guessing a version-pinned Uno app scaffold.

1. Install the current official Uno template and generate a WebAssembly app named `UnoPupusas`.
2. Replace the generated `MainPage.xaml` and `MainPage.xaml.cs` with the sample files.
3. Run `dotnet publish -f net10.0-browserwasm -c Release -o ./publish` in that Uno project.
4. Deploy `publish/wwwroot/` to a trusted HTTPS origin, e.g. `https://static.example.com/uno-pupusas/`.
5. Register its UI provider:

```csharp
using Unisono.UI.Uno;

var provider = new UnoUiProvider(
    "publish/wwwroot",
    new Uri("https://static.example.com/uno-pupusas/"))
    .Add("ui://pupuseria/uno");

ui.AddProvider(provider)
  .MapTool("calculate_pupusas", "ui://pupuseria/uno", "uno");
```

Use `dotnet new unoapp` or Uno's project wizard to obtain its current package versions. See [Uno WebAssembly publishing](https://platform.uno/docs/articles/uno-publishing-webassembly.html).

## React + TypeScript + Vite

The [React Pupusas](../samples/ReactPupusas/README.md) example uses `vite-plugin-singlefile` to put the component and MCP Apps SDK bridge into one `dist/index.html`. Register it directly without a static host:

```csharp
using Unisono.UI.React;
var provider = new ReactUiProvider("samples/ReactPupusas/dist")
    .Add("ui://pupuseria/order");
ui.AddProvider(provider).MapTool("calculate_pupusas", "ui://pupuseria/order", "react");
```

A React application with external assets can still use `ReactUiProvider(path, publicAssetBaseUrl)` and explicit CSP resource domains. Uno and Blazor generally need static WASM runtime hosting.

## Design details

- **No frontend runtime dependency in the provider package.** The provider runs on server-side .NET 10 and loads already-published assets; it does not compile Uno or Blazor at runtime.
- **One UI mapping per tool in the current registry.** To choose Uno vs. Blazor vs. React per host, build distinct mappings/registries or extend the UI registry to support variants.
- **One MCP host per registry.** The mapping is written as `_meta.ui.resourceUri` onto the registry's shared MCP tool objects, so a second host created from the same registry inherits the UI metadata.
- **Correct HTML base URL:** The provider replaces or inserts a `<base href="https://.../">` tag so relative framework assets load from the static origin in a sandboxed iframe.
- **Security:** Asset URLs require HTTPS except for loopback development. The MCP Apps resource handler declares the asset origin in `_meta.ui.csp.resourceDomains` and leaves `connectDomains` to the host if the UI makes network calls. The host can impose stricter CSP. Serve framework JS/WASM with appropriate CORS and MIME types.
- **Actual tool calls:** React uses the MCP Apps SDK; Blazor and Uno sample buttons call through the shared JS bridge. UI host support and client capabilities must be verified in deployment.
- **Size:** WebAssembly runtime files can be large for tiny cards; use plain HTML provider when startup speed matters.

## What still needs testing and production hardening

1. Build and run the `.NET 10`, Vite and Uno scaffold samples in a supported environment.
2. Verify sandbox loading, cross-origin bootstrap, lifecycle, CSP, host capability negotiation and tool calls in real ChatGPT and Claude hosts.
3. Add OAuth, authorization, rate limits, and HTTPS public hosting for production connectors.
4. Consider single-file HTML bundling to improve compatibility with hosts that restrict external runtime scripts and WebAssembly.
5. Add automated host end-to-end tests and plugin distribution workflows.

The .NET adapters, the host sample, the Blazor publish and the React/bridge bundles are covered by `.github/workflows/mcp-apps-ui-ci.yml`. The Uno page is still a drop-in for a current official Uno scaffold, and real-host (ChatGPT/Claude) rendering is not established.
