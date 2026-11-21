# Unisono.UI.Catalog

Declarative UI for [Unisono](https://github.com/egarim/unisono) tools: the server authors a JSON
document, one small trusted renderer draws it, and the model can only compose the components you allow.

Reference the project today (the package is not published yet; the UI provider packages are source-only
until they have survived a release cycle):

```xml
<ProjectReference Include="src/Unisono.UI.Catalog/Unisono.UI.Catalog.csproj" />
```

```csharp
var tools = new ToolRegistry().AddToolsFrom(new PupusaTools());

var ui = new UnisonoUiRegistry(tools)
    .AddProvider(new CatalogUiProvider()
        .AddFromTool(tools, "calculate_pupusas", "ui://pupuseria/order",
            form => form.Title("Pupusa Party").SubmitLabel("Calculate")))
    .MapTool("calculate_pupusas", "ui://pupuseria/order", CatalogUiProvider.FrameworkName);

await using var host = new UnisonoMcpHttpHost(tools, new UnisonoMcpHttpOptions { Port = 8792 }, ui: ui);
```

The document is derived from the tool's MCP input schema, so descriptions, required parameters,
enums, formats and defaults are declared once in C#:

- `string` → `TextInput` (a `date`/`email` format is passed through)
- `integer` / `number` → `NumberInput` (step 1 / 0.01, `minimum`/`maximum` honoured)
- `boolean` → `Checkbox`
- string enum → `Select`
- arrays and objects → refused with a clear error; use a React, Blazor or Uno provider for those

A submit button calls the tool with the bound values, and the result is rendered as text, a table
(JSON arrays), or a key/value table (small JSON objects).

## Document

```json
{
  "version": "1.0",
  "createSurface": {
    "surfaceId": "calculate_pupusas",
    "catalogId": "https://unisono.dev/ui/catalog/v1",
    "components": [
      { "id": "root", "component": "Column", "children": ["field-quantity", "submit", "result"] },
      { "id": "field-quantity", "component": "NumberInput", "label": "Number of pupusas",
        "value": { "@path": "/quantity" } },
      { "id": "submit", "component": "Button", "label": "Calculate",
        "action": { "name": "calculate_pupusas", "arguments": { "quantity": { "@path": "/quantity" } } } },
      { "id": "result", "component": "Text", "text": { "@path": "/_result/text" } }
    ],
    "dataModel": { "quantity": 3 }
  }
}
```

[`catalog.v1.json`](catalog.v1.json) is the JSON Schema for the envelope and the 12 components:
`Column`, `Row`, `Text`, `TextInput`, `NumberInput`, `Select`, `Checkbox`, `Button`, `List`,
`Table`, `Image`, `Divider`.

## Rendering and security

`CatalogUiProvider` serves one self-contained HTML page per tool: the renderer plus the document as
JSON. No external scripts, styles, fonts or images, so `_meta.ui.csp` can stay empty. The renderer
writes every value with `textContent` (never `innerHTML`), refuses unknown components and documents
from another catalog, and talks to the host over the MCP Apps `postMessage` protocol
(`ui/initialize`, `tools/call`, `ui/notifications/tool-result`).

Values are data, not code: there is no `eval`, no expression language and no network access beyond
the tool call. Business rules stay in the C# tool; the document only describes the form.

## Limitations

- Real ChatGPT/Claude iframe and CSP behaviour is not verified yet.
- The renderer is intentionally small: no layout freedom, animation, uploads, navigation or
  streaming updates. Use the React/Blazor/Uno providers when a tool needs those.
- The document is generated from the input schema; per-field overrides today are limited to title,
  submit label and labels.
