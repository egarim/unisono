import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { viteSingleFile } from "vite-plugin-singlefile";

// MCP Apps hosts often restrict loading external scripts.
// Inline React's JS and CSS into dist/index.html, so the ui:// resource
// is self-contained. An asset URL is not required for this example.
// Any future images/fonts/network requests still need CSP and hosting.
export default defineConfig({
  plugins: [react(), viteSingleFile()],
  build: {
    cssCodeSplit: false,
    assetsInlineLimit: 100000000
  }
});
