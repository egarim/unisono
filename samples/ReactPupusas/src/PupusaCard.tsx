import type { App } from "@modelcontextprotocol/ext-apps";
import { useApp } from "@modelcontextprotocol/ext-apps/react";
import { useState } from "react";

const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

function readNumber(result: Awaited<ReturnType<App["callServerTool"]>>): number {
  if (result.isError) throw new Error("The server rejected this calculation.");
  const first = result.content?.find((part) => part.type === "text");
  if (!first || first.type !== "text") throw new Error("Expected text from calculate_pupusas.");
  const value = Number(first.text.replace(/^"|"$/g, ""));
  if (!Number.isFinite(value)) throw new Error("Server returned a nonnumeric total.");
  return value;
}

export function PupusaCard() {
  const [quantity, setQuantity] = useState(3);
  const [priceEach, setPriceEach] = useState(1.75);
  const [total, setTotal] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState<string | null>(null);

  const { app, error } = useApp({
    appInfo: { name: "Unisono React Pupusas", version: "0.1.0" },
    capabilities: {},
    onAppCreated: (instance) => {
      instance.onerror = console.error;
      instance.ontoolinput = async () => { setStatus(null); };
      instance.ontoolresult = async (result) => {
        try { setTotal(readNumber(result)); }
        catch { /* another tool result may have different shape */ }
      };
    },
  });

  const calculate = async () => {
    if (!app) return;
    setBusy(true);
    setStatus(null);
    try {
      const response = await app.callServerTool({
        name: "calculate_pupusas",
        arguments: { quantity, priceEach },
      });
      setTotal(readNumber(response));
    } catch (cause) {
      setStatus(cause instanceof Error ? cause.message : "Unable to call the MCP tool.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="card">
      <p className="eyebrow">UNISONO · REACT MCP APP</p>
      <h1>🫓 The Pupusa Party</h1>
      <p>Ask the C# server to calculate the bill.</p>
      <label htmlFor="quantity">Number of pupusas</label>
      <input id="quantity" type="number" min="0" step="1" value={quantity}
        onChange={(e) => { setQuantity(Math.max(0, Math.floor(Number(e.target.value) || 0))); setTotal(null); }} />
      <label htmlFor="price">Price each (USD)</label>
      <input id="price" type="number" min="0" step="0.25" value={priceEach}
        onChange={(e) => { setPriceEach(Math.max(0, Number(e.target.value) || 0)); setTotal(null); }} />

      <button type="button" disabled={!app || busy} onClick={calculate}>
        {busy ? "Asking the kitchen…" : "Calculate with C# MCP"}
      </button>
      <section className="total" aria-live="polite">
        <span>Server total</span><strong>{total === null ? "—" : money.format(total)}</strong>
      </section>
      {error && <p role="alert">MCP Apps host unavailable: {error.message}</p>}
      {status && <p role="alert">{status}</p>}
      {!app && !error && <p className="note">Connecting to an MCP Apps host…</p>}
      <p className="note">
        This card must be launched by an MCP Apps host; a plain browser preview has no host bridge.
      </p>
    </main>
  );
}
