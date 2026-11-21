using System.ComponentModel;
using ModelContextProtocol.Server;
using Unisono;
using Unisono.Mcp;
using Unisono.McpApps;
using Unisono.UI.Blazor;
using Unisono.UI.React;
using Unisono.UI.Uno;
using Unisono.UI.Catalog;

// dotnet run --project samples/McpAppsPupusasHost -- react samples/ReactPupusas/dist
// dotnet run --project samples/McpAppsPupusasHost -- catalog
var frameworks = new[] { "react", "blazor", "uno", "catalog" };
var usage = "Usage: McpAppsPupusasHost <react|blazor|uno|catalog> [published-assets-folder] [public-assets-https-url]";
if (args.Length < 1 || !frameworks.Contains(args[0], StringComparer.OrdinalIgnoreCase))
{
    Console.Error.WriteLine(usage);
    return;
}

var framework = args[0].ToLowerInvariant();
var needsFolder = !string.Equals(framework, "catalog", StringComparison.Ordinal);
var needsAssetUri = framework is "blazor" or "uno";
if ((needsFolder && args.Length < 2) ||
    (needsAssetUri && (args.Length < 3 || !Uri.TryCreate(args[2], UriKind.Absolute, out _))) ||
    (framework == "react" && args.Length >= 3 && !Uri.TryCreate(args[2], UriKind.Absolute, out _)))
{
    Console.Error.WriteLine(usage);
    return;
}

var root = needsFolder ? Path.GetFullPath(args[1]) : null;
Uri? assetsUri = args.Length >= 3 ? new Uri(args[2]) : null;
const string resourceUri = "ui://pupuseria/order";
var tools = new ToolRegistry().AddToolsFrom(new PupusaTools());
IUnisonoUiProvider provider = framework switch
{
    "react" => (assetsUri is null ? new ReactUiProvider(root!) : new ReactUiProvider(root!, assetsUri)).Add(resourceUri),
    "blazor" => new BlazorUiProvider(root!, assetsUri!).Add(resourceUri),
    "uno" => new UnoUiProvider(root!, assetsUri!).Add(resourceUri),
    "catalog" => new CatalogUiProvider().AddFromTool(
        tools,
        "calculate_pupusas",
        resourceUri,
        form => form.Title("Pupusa Party").SubmitLabel("Calculate")),
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
    ? framework == "catalog"
        ? "Self-contained catalog document; no static asset host required."
        : "Self-contained HTML resource; no static asset host required."
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
