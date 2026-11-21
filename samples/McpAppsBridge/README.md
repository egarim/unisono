# Unisono MCP Apps browser bridge

Builds an **IIFE browser bundle** from the official MCP Apps SDK, for C# WebAssembly frontends (Blazor/Uno). It exposes `window.UnisonoMcpBridge.connect()` and `calculatePupusas(quantity, priceEach)` as promises.

```bash
cd samples/McpAppsBridge
npm install
npm run build
```

Copy `dist/unisono-mcp-bridge.js` into the published UI's static asset root and load it with a classic `<script src="unisono-mcp-bridge.js"></script>` tag **before the UI's framework startup**. The host must serve the entire UI site under its HTTPS asset origin and permit that origin in MCP Apps CSP.

The bundle uses host-only postMessage transport; **there is no fallback direct HTTP request** to the MCP endpoint, and the bridge will not connect when opening the page as a normal browser tab.

This is currently an example bridge with one `calculatePupusas` helper. A generic typed tool invoker and native Uno callback wiring are future improvements.
