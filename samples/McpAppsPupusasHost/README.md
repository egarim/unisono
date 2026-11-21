# Pupusa Party — real MCP Apps resource host

Unlike the local HTML playground, this sample **registers a C# tool and an actual MCP UI resource** in `UnisonoMcpHttpHost`. Select a UI framework at startup. The existing C# business action is unchanged.

## 1. Build the React UI

```bash
cd samples/ReactPupusas
npm install
UNISONO_UI_BASE=https://YOUR_STATIC_HOST/react-pupusas/ npm run build
```

Deploy `dist/` **contents** to the public URL in `UNISONO_UI_BASE`. If using a local development asset host, use `UNISONO_UI_BASE=http://127.0.0.1:5173/` for a matching Vite dev server while testing; configure MCP Apps CSP and access carefully.

## 2. Start the C# MCP server

From the repository root:

```bash
dotnet run --project samples/McpAppsPupusasHost -- react samples/ReactPupusas/dist https://YOUR_STATIC_HOST/react-pupusas/
```

The server listens at `http://127.0.0.1:8792/mcp` by default. Tool `calculate_pupusas` now advertises `_meta.ui.resourceUri = "ui://pupuseria/order"` and the server exposes a matching resource via `resources/list` and `resources/read`. Tool invocation continues to work headlessly.

For Blazor or Uno, publish the WASM UI and substitute the framework name, published `wwwroot/` directory and corresponding static asset URL.

## 3. Integrate with a host

Run an MCP Apps-compatible local host or connect a supported remote MCP endpoint. **A local loopback URL generally cannot serve as a public ChatGPT/Claude connector**; expose the server through an HTTPS deployment with authentication and appropriate origin controls.

React calls the C# `calculate_pupusas` tool through the official MCP Apps SDK. Blazor and Uno use the shared `samples/McpAppsBridge` bundle; build it, copy `unisono-mcp-bridge.js` into their static asset roots, and include its script before starting the WASM runtime.

## 4. Verification

Use the MCP smoke script for the headless tool, then inspect `tools/list` for `_meta.ui.resourceUri`, `resources/list` for `ui://pupuseria/order`, and `resources/read` for `text/html;profile=mcp-app`.

**Not yet verified:** React/Blazor/Uno iframe CSP permissions, frontend initial load times, and ChatGPT/Claude end-to-end interactions must be tested with each real host. The Uno app is still a drop-in page for an official Uno scaffold.

This sample is intended for development, not a production public connector. Production deployments require OAuth, authentication and per-tool authorization.
