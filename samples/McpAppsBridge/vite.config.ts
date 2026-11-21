import { defineConfig } from "vite";
import { resolve } from "node:path";

export default defineConfig({
  build: {
    lib: {
      entry: resolve(__dirname, "src/bridge.ts"),
      name: "UnisonoMcpBridge",
      formats: ["iife"],
      fileName: () => "unisono-mcp-bridge.js"
    }
  }
});
