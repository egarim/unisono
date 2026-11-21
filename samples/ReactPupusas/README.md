# React Pupusas — one C# tool, one interactive MCP App 🫓

This example uses **React + TypeScript + Vite**, but React is completely optional in Unisono.

## Step 1. Build a self-contained MCP App

Requires Node.js and npm:

```bash
cd samples/ReactPupusas
npm install
npm run build
```

The Vite single-file plugin embeds the app's JavaScript and CSS into **`dist/index.html`**. That file can be returned directly by the MCP server as a `ui://` resource without a public static-asset origin.

For local browser development you can run `npm run dev`, but the component will show **connecting to an MCP Apps host**. A normal website doesn't provide the host-side MCP postMessage protocol.

## Step 2. Start the Unisono MCP server

From the repository root:

```bash
dotnet run --project samples/McpAppsPupusasHost -- react samples/ReactPupusas/dist
```

This server binds the C# `calculate_pupusas` tool to the React UI and implements:

- `tools/list` with `_meta.ui.resourceUri`.
- `resources/list` and `resources/read` for `ui://pupuseria/order`.
- `tools/call`, unchanged for clients without interactive UI support.

Use the protocol smoke test:

```bash
python3 scripts/mcp-ui-smoke.py http://127.0.0.1:8792/mcp
```

## Step 3. Let the React component call C#

The component uses the official `@modelcontextprotocol/ext-apps` React hook `useApp` and calls:

```typescript
const result = await app.callServerTool({
  name: "calculate_pupusas",
  arguments: { quantity: 3, priceEach: 1.75 }
});
```

That request routes through the MCP Apps host to the same C# method used by regular MCP clients. The example parses the returned numeric text and displays the server-calculated total. It does **not** silently fall back to a local calculation if the host is absent.

## Step 4. Publish to real assistant hosts

For ChatGPT or Claude, use a compatible host and an authenticated HTTPS MCP server. Local loopback addresses generally aren't publicly reachable. Each host may enforce additional CSP, permissions, and review requirements.

The adapter is still available if you want to host a multipage or externally loaded React site:

```csharp
using Unisono.UI.React;
var provider = new ReactUiProvider(
    "samples/ReactPupusas/dist")
    .Add("ui://pupuseria/order");
```

Or use `new ReactUiProvider(distPath, httpsAssetBaseUri)` for a multi-file frontend with external resources.

**Validation:** Code and CI checks are included in the PR, but real ChatGPT/Claude end-to-end rendering and review approval have not been verified.
