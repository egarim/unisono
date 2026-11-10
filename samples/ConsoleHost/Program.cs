// Smallest possible Unisono host: two tools, served over MCP on localhost.
// Also used by scripts/mcp-smoke.sh to exercise the HTTP host without a phone.
using System.ComponentModel;
using ModelContextProtocol.Server;
using Unisono;
using Unisono.Mcp;

var port = args.Length > 0 ? int.Parse(args[0]) : 8788;

var registry = new ToolRegistry().AddToolsFrom(new DemoTools());
await using var host = new UnisonoMcpHttpHost(registry, new UnisonoMcpHttpOptions
{
    Port = port,
    ServerName = "unisono-console",
    Instructions = "Demo tools for Unisono.",
});
host.Start();
Console.WriteLine($"Unisono console host at {host.Url} (tools: {string.Join(", ", registry.Names)}). Ctrl+C to stop.");

var done = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.TrySetResult(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => done.TrySetResult();
await done.Task;

[McpServerToolType]
sealed class DemoTools
{
    [McpServerTool(Name = "echo", ReadOnly = true, Destructive = false)]
    [Description("Returns the text it was given.")]
    public string Echo([Description("Text to echo back")] string text) => text;

    [McpServerTool(Name = "add", ReadOnly = true, Destructive = false)]
    [Description("Adds two numbers.")]
    public double Add([Description("First number")] double a, [Description("Second number")] double b) => a + b;
}
