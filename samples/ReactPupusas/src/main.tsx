import React from "react";
import { createRoot } from "react-dom/client";
import { PupusaCard } from "./PupusaCard";
import "./style.css";

const root = document.getElementById("root");
if (!root) throw new Error("Missing #root element");

createRoot(root).render(
  <React.StrictMode>
    <PupusaCard />
  </React.StrictMode>
);
