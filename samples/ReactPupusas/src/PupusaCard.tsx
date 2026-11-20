import { useState } from "react";

const money = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
});

export function PupusaCard() {
  const [quantity, setQuantity] = useState(3);
  const [priceEach, setPriceEach] = useState(1.75);
  const total = Math.max(0, quantity) * Math.max(0, priceEach);

  return (
    <main className="card">
      <p className="eyebrow">UNISONO · REACT ADAPTER</p>
      <h1>🫓 The Pupusa Party</h1>
      <p>How many pupusas does happiness cost?</p>
      <label htmlFor="quantity">Number of pupusas</label>
      <input
        id="quantity"
        type="number"
        min="0"
        step="1"
        value={quantity}
        onChange={(e) => {
          const next = Number(e.target.value);
          setQuantity(Number.isFinite(next) ? Math.max(0, Math.floor(next)) : 0);
        }}
      />
      <label htmlFor="price">Price each (USD)</label>
      <input
        id="price"
        type="number"
        min="0"
        step="0.25"
        value={priceEach}
        onChange={(e) => {
          const next = Number(e.target.value);
          setPriceEach(Number.isFinite(next) ? Math.max(0, next) : 0);
        }}
      />
      <section className="total" aria-live="polite">
        <span>Party total</span>
        <strong>{money.format(total)}</strong>
      </section>
      <p className="note">
        Browser-only preview: this calculation runs locally in React.
        Connecting to the C# MCP tool needs the MCP Apps host bridge.
      </p>
    </main>
  );
}
