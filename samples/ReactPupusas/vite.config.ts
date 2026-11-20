import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// IMPORTANT for MCP Apps: the HTML UI resource does not live on the static
// asset server. Vite must emit asset URLs targeting the actual deployed folder.
// Example:
// UNISONO_UI_BASE=https://static.example.com/react-pupusas/ npm run build
const assetBase = process.env.UNISONO_UI_BASE || "/";

if (
  assetBase !== "/" &&
  !/^https:\/\/[^?#]+\/$/.test(assetBase) &&
  !/^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?\/[^?#]*$/.test(assetBase)
) {
  throw new Error(
    "UNISONO_UI_BASE must be an HTTPS URL ending in / (or a local HTTP URL for development)."
  );
}

export default defineConfig({
  plugins: [react()],
  base: assetBase,
});
