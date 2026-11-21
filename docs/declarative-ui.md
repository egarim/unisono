# Declarative UI: a document, one renderer 🫓

`Unisono.UI.Catalog` is the zero-frontend path: a tool's interface is a **JSON document** generated
from the tool itself, drawn by one small renderer. No React, Blazor, Uno, build step or static host.
The server owns the vocabulary through a fixed **catalog**, so generated UI cannot drift from the set
of components you allow.

This is the counterpart of the [published WASM/Vite providers](uno-blazor-ui-providers.md): use the
catalog for forms, lists and tables; use a provider when a tool needs a real application.

## Three pieces

| Piece | What it is | Where |
|---|---|---|
| **Document** | `createSurface` with components, data bindings and actions | `UnisonoUiDocument`, emitted by `UnisonoUiDocumentBuilder` |
| **Renderer** | One self-contained HTML page: CSS, JS, the document | `renderer.html` + `renderer.js`, served by `CatalogUiProvider` |
| **Binding** | Fields from the MCP input schema, a submit action per tool, a result area | The tool's `[McpServerTool]` method, unchanged |

## Use it

```csharp
var tools = new ToolRegistry().AddToolsFrom(new PupusaTools());

var ui = new UnisonoUiRegistry(tools)
    .AddProvider(new CatalogUiProvider()
        .AddFromTool(tools, "calculate_pupusas", "ui://pupuseria/order",
            form => form.Title("Pupusa Party").SubmitLabel("Calculate")))
    .MapTool("calculate_pupusas", "ui://pupuseria/order", CatalogUiProvider.FrameworkName);

await using var host = new UnisonoMcpHttpHost(tools, options, ui: ui);
```

Try it without writing any UI code:

```bash
dotnet run --project samples/McpAppsPupusasHost -- catalog
python3 scripts/mcp-ui-smoke.py http://127.0.0.1:8792/mcp
```

Or see the generated document and a browser preview in the playground:

```bash
dotnet run --project samples/UiPlayground -- --catalog
```

## The document

The envelope follows A2UI v1.0 (`version`, `createSurface`, adjacency-list components); the component
vocabulary is the Unisono v1 catalog. Bindings are JSON Pointers: `{"@path": "/quantity"}`.

```json
{
  "version": "1.0",
  "createSurface": {
    "surfaceId": "calculate_pupusas",
    "catalogId": "https://unisono.dev/ui/catalog/v1",
    "components": [
      { "id": "root", "component": "Column", "gap": "md",
        "children": ["title", "field-quantity", "submit", "result"] },
      { "id": "title", "component": "Text", "text": "Pupusa Party", "variant": "title" },
      { "id": "field-quantity", "component": "NumberInput", "label": "Number of pupusas",
        "value": { "@path": "/quantity" }, "required": true, "step": 1 },
      { "id": "submit", "component": "Button", "label": "Calculate", "variant": "primary",
        "action": { "name": "calculate_pupusas",
                    "arguments": { "quantity": { "@path": "/quantity" } } } },
      { "id": "result", "component": "Text", "text": { "@path": "/_result/text" } }
    ],
    "dataModel": { "quantity": 3 }
  }
}
```

### Components

| Component | Required | Notes |
|---|---|---|
| `Column`, `Row` | `children` | layout only; `gap` is `sm`/`md`/`lg` |
| `Text` | `text` | `variant` is `title`, `body` or `caption` |
| `TextInput` | `label`, `value` | `format` `date`/`email` is passed through |
| `NumberInput` | `label`, `value` | `min`, `max`, `step` |
| `Select` | `label`, `value`, `options` | `[{ "value", "label" }]` |
| `Checkbox` | `label`, `value` | |
| `Button` | `label`, `action` | action is a tool name plus bound arguments |
| `Table` | `rows` | `columns` optional; derived from the first rows |
| `List` | `items` | `titleField`/`subtitleField` for object items |
| `Image` | `src` | host CSP applies |
| `Divider` | — | |

[`catalog.v1.json`](../src/Unisono.UI.Catalog/catalog.v1.json) is the JSON Schema.

## Generated from the tool

`UnisonoUiDocumentBuilder.For(tools, "add_task")` reads the MCP input schema; `Title`, `SubmitLabel`
and `Label` override presentation only:

| Input schema | Component |
|---|---|
| `string` | `TextInput` (format passed through) |
| `integer` / `number` | `NumberInput` (step 1 / 0.01, min/max) |
| `boolean` | `Checkbox` |
| string `enum` | `Select` |
| `array` / `object` | refused with a clear error: use a provider for those tools |

Descriptions become labels, the `required` array marks required fields, `default` seeds the data
model. The tool and its C# method stay the only source of business rules.

## Results

The renderer puts the tool result into `/_result`: `text` (numbers, strings, small objects formatted
as key/value rows) and `rows` (JSON arrays, drawn by the result `Table`). `structuredContent` wins
over text when both are present. Nothing else in the data model is overwritten.

## Protocol and security

The renderer speaks the MCP Apps postMessage protocol directly, with no SDK or external script:

- `ui/initialize` → host context → `ui/notifications/initialized`
- `tools/call` when the user submits
- `ui/notifications/tool-input` and `ui/notifications/tool-result` from the host

Security rules:

- Values are rendered with `textContent`; the renderer never uses `innerHTML`, `eval` or a remote
  script, so the MCP Apps CSP can stay empty (`_meta.ui.csp` unset).
- The document is data: no expressions, no network, no host APIs beyond the tool call.
- The renderer refuses unknown components, documents from another `catalogId`, and documents
  without a `root`.
- Tool authorization stays on the server; the iframe never receives credentials.
- Opening the page in a plain browser tab shows a preview notice and disables the submit button
  instead of pretending to call a tool.

## Limitations

- Real ChatGPT/Claude iframe and CSP behaviour is not verified yet; the protocol side is tested with a
  simulated host ([`tests/CatalogRenderer`](../tests/CatalogRenderer)).
- No layout freedom, animation, uploads, navigation or streaming updates in v1. Use a provider.
- Per-field overrides are limited to title, submit label and labels.
- One tool per document; a document mapping more than one tool is future work.
- The catalog id and component set are versioned together; adding components without breaking v1 is
  the intended way to grow.
