# Plug in Uno, Blazor, or React 🫓

**Write your UI in C# + XAML (Uno) or C# + Razor (Blazor). Keep the same Unisono tool.**

This feature introduces three providers:

- `Unisono.UI.Uno.UnoUiProvider`
- `Unisono.UI.Blazor.BlazorUiProvider`
- `Unisono.UI.React.ReactUiProvider`

All three implement `IUnisonoUiProvider` through `PublishedWebUiProvider`. They are **publish-output adapters**: Uno and Blazor load a published WebAssembly `wwwroot/index.html`, while React loads Vite's `dist/index.html`. The JS, CSS, WASM, and other assets must be hosted at a trusted HTTPS origin.

**Scope (important):** This does *not yet* implement MCP `resources/read`, tool `_meta.ui.resourceUri`, host security metadata, or MCP Apps JavaScript bridging. The adapter does not make an iframe work by itself. It allows a future protocol layer to obtain the right UI resource, preserving separation from any UI toolkit.

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
// The future MCP Apps resource handler will return resource.Content as
// text/html;profile=mcp-app, together with suitable resource CSP metadata.
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

See [React Pupusas](../samples/ReactPupusas/README.md) for an end-to-end example, including `npm install`, Vite production builds, and React provider registration. Configure `UNISONO_UI_BASE` to the final static asset URL when building: Vite emits script and CSS asset URLs based on its build-time base. Rewriting only the HTML `<base>` element does **not** correct Vite's absolute asset paths.

```csharp
using Unisono.UI.React;

var provider = new ReactUiProvider(
    "samples/ReactPupusas/dist",
    new Uri("https://static.example.com/react-pupusas/"))
    .Add("ui://pupuseria/react");

ui.AddProvider(provider)
  .MapTool("calculate_pupusas", "ui://pupuseria/react", "react");
```

## Design details

- **No frontend runtime dependency in the provider package.** The provider runs on server-side .NET 10 and loads already-published assets; it does not compile Uno or Blazor at runtime.
- **One UI mapping per tool in the current registry.** To choose Uno vs. Blazor vs. React per host, build distinct mappings/registries or extend the UI registry to support variants.
- **Correct HTML base URL:** The provider replaces or inserts a `<base href="https://.../">` tag so relative framework assets load from the static origin in a sandboxed iframe.
- **Security:** Asset URLs require HTTPS except for loopback development. The future MCP Apps resource handler must declare the asset origin in `_meta.ui.csp.resourceDomains`, and relevant `connectDomains` if the UI makes network calls. The host can impose stricter CSP. Serve framework JS/WASM with appropriate CORS and MIME types.
- **HTML isn't enough for tool calls:** Real MCP Apps interactions require a JavaScript host bridge, secure command handling, and capability negotiation. Both UI samples deliberately calculate the total locally.
- **Size:** WebAssembly runtime files can be large for tiny cards; use plain HTML provider when startup speed matters.

## To finish MCP Apps integration

1. Add `resources/list` and `resources/read` mappings to `UnisonoMcpHttpHost`.
2. Add `_meta.ui.resourceUri` to mapped tool descriptors.
3. Emit CSP metadata for `AssetBaseUri`.
4. Implement MCP Apps JavaScript bridge and adapter integration (Uno + Blazor + React).
5. Test sandbox loading, cross-origin bootstrap, lifecycle, tool calls, and headless fallback in real host environments.

These projects and samples were authored without a verified .NET/npm build in the editing environment; compile/publish tests are still required.
