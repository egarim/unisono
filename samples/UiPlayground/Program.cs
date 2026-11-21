// UI Playground: one C# tool, one optional UI, zero frontend frameworks.
// This example stops at the registry and a local preview; pass the registry to
// the host (see samples/McpAppsPupusasHost) to serve the resource over MCP.
using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using Unisono;
using Unisono.Mcp;
using Unisono.McpApps;

const string toolName = "calculate_pupusas";
const string resourceUri = "ui://pupuseria/order-card";

// First, give the assistant an ordinary C# tool.
var tools = new ToolRegistry().AddToolsFrom(new PupusaTools());

// Next, register an optional UI provider. This one serves prebuilt HTML;
// a future Uno or Blazor adapter would implement the same IUnisonoUiProvider.
var html = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "order-card.html"));
var provider = new StaticHtmlUiProvider().Add(resourceUri, html);
var ui = new UnisonoUiRegistry(tools)
    .AddProvider(provider)
    .MapTool(toolName, resourceUri, "html", "Pupusa Party Calculator");

// The UI is optional: headless clients still invoke the same tool.
var total = await tools.InvokeAsync<decimal>(
    toolName,
    new System.Text.Json.Nodes.JsonObject { ["quantity"] = 3, ["priceEach"] = 1.75m });
Console.WriteLine($"Three pupusas cost ${total:0.00}.");

// Resolve the UI resource and save a local preview to inspect in a browser.
// Serving it to a real host is one line away: new UnisonoMcpHttpHost(tools, ui: ui).
var capabilities = new UnisonoUiHostCapabilities(SupportsMcpApps: true);
if (ui.CanRender(toolName, capabilities))
{
    var asset = await ui.GetAssetAsync(toolName);
    var preview = Path.Combine(Environment.CurrentDirectory, "pupusa-preview.html");
    await File.WriteAllTextAsync(preview, Encoding.UTF8.GetString(asset.Content.Span));
    Console.WriteLine($"UI mapping: {toolName} → {asset.ResourceUri} ({asset.MimeType})");
    Console.WriteLine($"Browser-only preview: {preview}");
}

await using var host = new UnisonoMcpHttpHost(tools, new UnisonoMcpHttpOptions
{
    Port = 8791,
    ServerName = "unisono-ui-playground",
    Instructions = "Calculate pupusa prices with the C# tool. This host serves tools only; the UI resource is previewable locally."
});
host.Start();
Console.WriteLine($"Headless MCP tools available at {host.Url}");
Console.WriteLine("Press Ctrl+C to stop.");
var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.TrySetResult(); };
await stop.Task;

[McpServerToolType]
sealed class PupusaTools
{
    [McpServerTool(Name = "calculate_pupusas", ReadOnly = true, Destructive = false)]
    [Description("Calculate the total price of pupusas.")]
    public decimal CalculatePupusas(
        [Description("Number of pupusas")] int quantity,
        [Description("Price per pupusa")] decimal priceEach)
    {
        if (quantity < 0 || priceEach < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity and price must be non-negative.");
        return quantity * priceEach;
    }
}
