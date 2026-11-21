"use strict";

const test = require("node:test");
const assert = require("node:assert");
const fs = require("node:fs");
const path = require("node:path");
const { JSDOM } = require("jsdom");

const rendererSource = fs.readFileSync(
  path.join(__dirname, "..", "..", "src", "Unisono.UI.Catalog", "renderer.js"),
  "utf8"
);

const CATALOG_ID = "https://unisono.dev/ui/catalog/v1";

function textDocument(text, title) {
  return {
    version: "1.0",
    createSurface: {
      surfaceId: title || "surface",
      catalogId: CATALOG_ID,
      components: [
        { id: "root", component: "Column", children: ["text"] },
        { id: "text", component: "Text", text }
      ],
      dataModel: {}
    }
  };
}

const pupusaDocument = {
  version: "1.0",
  createSurface: {
    surfaceId: "calculate_pupusas",
    catalogId: CATALOG_ID,
    components: [
      {
        id: "root",
        component: "Column",
        children: ["title", "field-quantity", "field-priceEach", "submit", "result", "result-table"],
        gap: "md"
      },
      { id: "title", component: "Text", text: "Pupusa Party", variant: "title" },
      { id: "field-quantity", component: "NumberInput", value: { "@path": "/quantity" }, label: "Number of pupusas", step: 1 },
      { id: "field-priceEach", component: "NumberInput", value: { "@path": "/priceEach" }, label: "Price each", step: 0.01 },
      {
        id: "submit",
        component: "Button",
        label: "Run",
        variant: "primary",
        action: {
          name: "calculate_pupusas",
          arguments: { quantity: { "@path": "/quantity" }, priceEach: { "@path": "/priceEach" } }
        }
      },
      { id: "result", component: "Text", text: { "@path": "/_result/text" }, variant: "body" },
      { id: "result-table", component: "Table", rows: { "@path": "/_result/rows" } }
    ],
    dataModel: { quantity: 3, priceEach: 1.75 }
  }
};

const addTaskDocument = {
  version: "1.0",
  createSurface: {
    surfaceId: "add_task",
    catalogId: CATALOG_ID,
    components: [
      { id: "root", component: "Column", children: ["field-title", "field-priority", "field-done", "submit", "result"] },
      { id: "field-title", component: "TextInput", value: { "@path": "/title" }, label: "The task title", required: true },
      {
        id: "field-priority",
        component: "Select",
        value: { "@path": "/priority" },
        label: "Priority",
        options: [
          { value: "low", label: "Low" },
          { value: "normal", label: "Normal" },
          { value: "high", label: "High" }
        ]
      },
      { id: "field-done", component: "Checkbox", value: { "@path": "/done" }, label: "Done" },
      {
        id: "submit",
        component: "Button",
        label: "Add",
        action: {
          name: "add_task",
          arguments: {
            title: { "@path": "/title" },
            priority: { "@path": "/priority" },
            done: { "@path": "/done" }
          }
        }
      },
      { id: "result", component: "Text", text: { "@path": "/_result/text" } }
    ],
    dataModel: { priority: "normal", done: false }
  }
};

function createWindow() {
  const dom = new JSDOM("<!doctype html><html><body><div id=\"root\"></div></body></html>", { runScripts: "outside-only" });
  dom.window.__UNISONO_CATALOG__ = { id: CATALOG_ID, version: "1.0" };
  dom.window.eval(rendererSource);
  return dom;
}

function fakeTransport(options) {
  options = options || {};
  const listeners = {};
  const calls = [];
  return {
    calls,
    inIframe: () => options.inIframe !== false,
    start() {},
    connect: async () => {
      if (options.failConnect) throw new Error("host refused");
      return {};
    },
    on(name, listener) {
      (listeners[name] = listeners[name] || []).push(listener);
    },
    callTool: async (name, args) => {
      calls.push({ name, args });
      if (options.callTool) return options.callTool(name, args);
      return { content: [{ type: "text", text: "5.25" }] };
    },
    emit(name, payload) {
      (listeners[name] || []).forEach((listener) => listener(payload));
    }
  };
}

async function startApp(documentData, options) {
  const dom = createWindow();
  const transport = fakeTransport(options);
  const root = dom.window.document.getElementById("root");
  const app = dom.window.UnisonoCatalog.createApp(root, documentData, transport);
  const connected = await app.start();
  return { dom, transport, root, app, connected, window: dom.window, doc: dom.window.document };
}

function tick(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms || 10));
}

test("renders fields from the document and prefills the data model", async () => {
  const { doc } = await startApp(pupusaDocument);

  const quantity = doc.getElementById("u-field-quantity");
  const price = doc.getElementById("u-field-priceEach");
  assert.ok(quantity, "quantity input exists");
  assert.equal(quantity.type, "number");
  assert.equal(quantity.value, "3");
  assert.equal(price.value, "1.75");
  assert.equal(doc.getElementById("u-submit").disabled, false);
  assert.equal(doc.getElementById("u-submit").textContent, "Run");
  assert.match(doc.body.textContent, /Pupusa Party/);
});

test("submits the bound arguments and shows the text result", async () => {
  const { doc, transport } = await startApp(pupusaDocument);

  const quantity = doc.getElementById("u-field-quantity");
  quantity.value = "4";
  quantity.dispatchEvent(new doc.defaultView.Event("input"));
  doc.getElementById("u-submit").click();
  await tick();

  assert.deepEqual(transport.calls[0], { name: "calculate_pupusas", args: { quantity: 4, priceEach: 1.75 } });
  const result = doc.getElementById("u-result");
  assert.equal(result.hidden, false);
  assert.equal(result.textContent, "5.25");
});

test("renders a JSON array result as a table", async () => {
  const tasks = [
    { title: "Milk", done: false },
    { title: "Eggs", done: true }
  ];
  const { doc } = await startApp(pupusaDocument, {
    callTool: async () => ({ content: [{ type: "text", text: JSON.stringify(tasks) }] })
  });

  doc.getElementById("u-submit").click();
  await tick();

  const table = doc.querySelector(".unisono-table");
  const rows = table.querySelectorAll("tbody tr");
  const headers = Array.from(table.querySelectorAll("thead th")).map((th) => th.textContent);
  assert.deepEqual(headers, ["Title", "Done"]);
  assert.equal(rows.length, 2);
  assert.match(table.textContent, /Milk/);
  assert.match(table.textContent, /Eggs/);
  assert.match(table.textContent, /Yes/);
  assert.match(table.textContent, /No/);
});

test("prefers structuredContent over text", async () => {
  const { doc } = await startApp(pupusaDocument, {
    callTool: async () => ({ content: [], structuredContent: 12.5 })
  });

  doc.getElementById("u-submit").click();
  await tick();

  assert.equal(doc.getElementById("u-result").textContent, "12.5");
});

test("applies an incoming tool-input notification to the form", async () => {
  const { doc, transport } = await startApp(pupusaDocument);

  transport.emit("toolinput", { arguments: { quantity: 7 } });

  assert.equal(doc.getElementById("u-field-quantity").value, "7");
});

test("applies an incoming tool-result notification", async () => {
  const { doc, transport } = await startApp(pupusaDocument);

  transport.emit("toolresult", { content: [{ type: "text", text: "9.5" }] });

  assert.equal(doc.getElementById("u-result").textContent, "9.5");
});

test("blocks submission when a required field is empty", async () => {
  const { doc, transport } = await startApp(addTaskDocument);

  doc.getElementById("u-submit").click();
  await tick();

  assert.equal(transport.calls.length, 0);
  assert.match(doc.body.textContent, /Please fill in: The task title/);
  assert.equal(doc.getElementById("u-field-title").getAttribute("aria-invalid"), "true");
});

test("sends enum, checkbox and text values from the form", async () => {
  const { doc, transport } = await startApp(addTaskDocument);

  const title = doc.getElementById("u-field-title");
  title.value = "Buy masa";
  title.dispatchEvent(new doc.defaultView.Event("input"));
  const priority = doc.getElementById("u-field-priority");
  priority.value = "high";
  priority.dispatchEvent(new doc.defaultView.Event("input"));
  const done = doc.getElementById("u-field-done");
  done.checked = true;
  done.dispatchEvent(new doc.defaultView.Event("input"));

  doc.getElementById("u-submit").click();
  await tick();

  assert.deepEqual(transport.calls[0], {
    name: "add_task",
    args: { title: "Buy masa", priority: "high", done: true }
  });
});

test("writes user text as text and never as markup", async () => {
  const payload = "<img src=x onerror=\"window.__xss = 1\"><b>bold?</b>";
  const { root } = await startApp(textDocument(payload));

  assert.equal(root.querySelectorAll("img").length, 0);
  assert.equal(root.querySelectorAll("b").length, 0);
  assert.match(root.textContent, /<img src=x/);
  assert.equal(root.ownerDocument.defaultView.__xss, undefined);
});

test("shows preview mode instead of calling a tool without a host", async () => {
  const { doc, transport, connected } = await startApp(pupusaDocument, { inIframe: false });

  assert.equal(connected, false);
  assert.match(doc.body.textContent, /Preview mode/);
  assert.equal(doc.getElementById("u-submit").disabled, true);
  assert.equal(transport.calls.length, 0);
});

test("reports an unreachable host", async () => {
  const { doc, connected } = await startApp(pupusaDocument, { failConnect: true });

  assert.equal(connected, false);
  assert.match(doc.body.textContent, /MCP Apps host unavailable: host refused/);
  assert.equal(doc.getElementById("u-submit").disabled, true);
});

test("performs the MCP Apps handshake and calls a tool over postMessage", async () => {
  const dom = createWindow();
  const sent = [];
  const host = {
    postMessage(message) {
      sent.push(message);
    }
  };
  const transport = dom.window.UnisonoCatalog.createTransport({ parent: host, timeoutMs: 1000 });
  transport.start();

  const connecting = transport.connect();
  const initialize = sent[0];
  assert.equal(initialize.method, "ui/initialize");
  assert.equal(initialize.params.protocolVersion, "2026-01-26");
  assert.equal(initialize.params.appInfo.name, "Unisono UI");

  dom.window.dispatchEvent(new dom.window.MessageEvent("message", {
    data: {
      jsonrpc: "2.0",
      id: initialize.id,
      result: { protocolVersion: "2026-01-26", hostInfo: { name: "test", version: "1" }, hostCapabilities: {}, hostContext: {} }
    },
    source: host
  }));
  await connecting;
  assert.equal(sent[1].method, "ui/notifications/initialized");

  const received = [];
  transport.on("toolresult", (result) => received.push(result));

  const calling = transport.callTool("calculate_pupusas", { quantity: 3, priceEach: 1.75 });
  const call = sent[2];
  assert.equal(call.method, "tools/call");
  assert.deepEqual(call.params, { name: "calculate_pupusas", arguments: { quantity: 3, priceEach: 1.75 } });

  dom.window.dispatchEvent(new dom.window.MessageEvent("message", {
    data: { jsonrpc: "2.0", id: call.id, result: { content: [{ type: "text", text: "5.25" }] } },
    source: host
  }));
  const result = await calling;
  assert.equal(result.content[0].text, "5.25");

  dom.window.dispatchEvent(new dom.window.MessageEvent("message", {
    data: { jsonrpc: "2.0", method: "ui/notifications/tool-result", params: { content: [{ type: "text", text: "7" }] } },
    source: host
  }));
  assert.equal(received.length, 1);
  assert.equal(received[0].content[0].text, "7");
});

test("surfaces a host error response", async () => {
  const dom = createWindow();
  const sent = [];
  const host = { postMessage: (message) => sent.push(message) };
  const transport = dom.window.UnisonoCatalog.createTransport({ parent: host, timeoutMs: 1000 });
  transport.start();

  const calling = transport.callTool("calculate_pupusas", {});
  const call = sent[0];
  dom.window.dispatchEvent(new dom.window.MessageEvent("message", {
    data: { jsonrpc: "2.0", id: call.id, error: { code: -32000, message: "tool not allowed" } },
    source: host
  }));

  await assert.rejects(calling, /tool not allowed/);
});

test("refuses an unknown component", () => {
  const dom = createWindow();
  const documentData = {
    version: "1.0",
    createSurface: {
      surfaceId: "s",
      catalogId: CATALOG_ID,
      components: [
        { id: "root", component: "Column", children: ["video"] },
        { id: "video", component: "Video", src: "x" }
      ]
    }
  };

  assert.throws(
    () => dom.window.UnisonoCatalog.createApp(dom.window.document.getElementById("root"), documentData, fakeTransport()),
    /Unknown component 'Video'/
  );
});

test("refuses a document from another catalog", () => {
  const dom = createWindow();
  const documentData = textDocument("hi");
  documentData.createSurface.catalogId = "https://example.com/other";

  assert.throws(
    () => dom.window.UnisonoCatalog.createApp(dom.window.document.getElementById("root"), documentData, fakeTransport()),
    /another catalog|serves/
  );
});

test("refuses a document without a root", () => {
  const dom = createWindow();
  const documentData = textDocument("hi");
  documentData.createSurface.components = [{ id: "text", component: "Text", text: "hi" }];

  assert.throws(
    () => dom.window.UnisonoCatalog.createApp(dom.window.document.getElementById("root"), documentData, fakeTransport()),
    /no root component/
  );
});
