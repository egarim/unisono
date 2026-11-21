using System.ComponentModel;
using ModelContextProtocol.Server;
using Unisono;
using Unisono.Mcp;
using Unisono.McpApps;
using Unisono.UI.Blazor;
using Unisono.UI.React;
using Unisono.UI.Uno;

// dotnet run --project samples/McpAppsPupusasHost -- react samples/ReactPupusas/dist
if (args.Length < 2 || !new[] { "react", "blazor", "uno" }.Contains(args[0], StringComparer.OrdinalIgnoreCase)
    || (!string.Equals(args[0], "react", StringComparison.OrdinalIgnoreCase) && args.Length < 3)
    || (args.Length >= 3 && !Uri.TryCreate(args[2], UriKind.Absolute, out _)))
{
    Console.Error.WriteLine("Usage: McpAppsPupusasHost <react|blazor|uno> <published-assets-folder> [public-assets-https-url]");
    return;
}

var framework = args[0].ToLowerInvariant();
var root = Path.GetFullPath(args[1]);
Uri? assetsUri = args.Length >= 3 ? new Uri(args[2]) : null;
const string resourceUri = "ui://pupuseria/order";
var tools = new ToolRegistry().AddToolsFrom(new PupusaTools());
IUnisonoUiProvider provider = framework switch
{
    "react" => (assetsUri is null ? new ReactUiProvider(root) : new ReactUiProvider(root, assetsUri)).Add(resourceUri),
    "blazor" => new BlazorUiProvider(root, assetsUri!).Add(resourceUri),
    "uno" => new UnoUiProvider(root, assetsUri!).Add(resourceUri),
    _ => throw new ArgumentOutOfRangeException(nameof(framework))
};

var ui = new UnisonoUiRegistry(tools)
    .AddProvider(provider)
    .MapTool("calculate_pupusas", resourceUri, framework, "Pupusa Party");

await using var host = new UnisonoMcpHttpHost(
    tools,
    new UnisonoMcpHttpOptions
    {
        Port = 8792,
        ServerName = "unisono-pupusas-mcp-app",
        Instructions = "Calculate pupusa totals and, if supported, show the interactive order card."
    },
    ui: ui);

host.Start();
Console.WriteLine($"MCP Apps host: {host.Url}");
Console.WriteLine($"Framework: {framework} | Resource: {resourceUri}");
Console.WriteLine(assetsUri is null
    ? "Self-contained HTML resource; no static asset host required."
    : "The static assets must already be reachable at " + assetsUri);
Console.WriteLine("Press Ctrl+C to stop.");
var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.TrySetResult(); };
await stop.Task;

[McpServerToolType]
sealed class PupusaTools
{
    [McpServerTool(Name = "calculate_pupusas", ReadOnly = true, Destructive = false)]
    [Description("Calculate the total price of a pupusa order.")]
    public decimal CalculatePupusas(
        [Description("Number of pupusas (zero or greater)")] int quantity,
        [Description("Price per pupusa (zero or greater)")] decimal priceEach)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (priceEach < 0) throw new ArgumentOutOfRangeException(nameof(priceEach));
        return quantity * priceEach;
    }
}
