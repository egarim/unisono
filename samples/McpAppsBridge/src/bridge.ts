import { App } from "@modelcontextprotocol/ext-apps";

/**
 * Shared browser bridge for Uno and Blazor UI components.
 * Script exposes window.UnisonoMcpBridge (IIFE bundle) and is loaded in
 * the MCP Apps iframe, never in the .NET server process.
 */
const app = new App({ name: "Unisono .NET UI Bridge", version: "0.1.0" });
app.onerror = console.error;
app.onteardown = async () => ({});

let connecting: Promise<void> | null = null;
export function connect(): Promise<void> {
  return connecting ??= app.connect();
}

export async function callTool(name: string, args: Record<string, unknown> = {}) {
  if (!name || !name.trim()) throw new Error("Tool name is required");
  await connect();
  return app.callServerTool({ name, arguments: args });
}

export async function calculatePupusas(quantity: number, priceEach: number): Promise<number> {
  const result = await callTool("calculate_pupusas", { quantity, priceEach });

  if (result.isError) throw new Error("MCP tool calculation failed");
  const text = result.content?.find(c => c.type === "text");
  if (!text || text.type !== "text") throw new Error("Expected numeric text content");
  const number = Number(text.text.replace(/^"|"$/g, ""));
  if (!Number.isFinite(number)) throw new Error("Invalid numeric response from MCP");
  return number;
}
