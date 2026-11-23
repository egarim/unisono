// The Vivarium companion: a read-only MCP view of one team, as the person whose machine is paired.
//
//   dotnet run --project samples/VivariumCompanion -- --team vivarium-nuvos
//
// It authenticates with the pairing this machine already has (creatura_device's state file: the same refresh grant
// the device daemon uses), so there is no new secret and no new server surface. Reads and one diagnose only:
// vivarium_what_is_blocking names the gates a project must pass, in order, each with the action that clears it.
// Signing, approvals and passkeys stay in the app - the companion never writes.
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Unisono;
using Unisono.Samples.VivariumCompanion;

string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var team = Arg("--team");
var statePath = Arg("--state")
    ?? VivariumClient.LocateState(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), team);
if (!File.Exists(statePath))
{
    Console.Error.WriteLine($"no pairing state at {statePath} - pair this machine first (setup.sh or setup.ps1)");
    return 1;
}

JsonObject state;
try
{
    // Share-read: the device daemon may be refreshing the same file in place beside us; and a corrupt file must say
    // so here, not die on the first tool call (review, 2026-11-23).
    using var fs = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    using var reader = new StreamReader(fs);
    state = JsonNode.Parse(reader.ReadToEnd()) as JsonObject
        ?? throw new JsonException("the pairing state is not a JSON object");
}
catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
{
    Console.Error.WriteLine($"the pairing state at {statePath} cannot be read ({e.Message}) - re-pair this machine");
    return 1;
}

var missing = VivariumClient.Missing(state);
if (missing.Count > 0)
{
    Console.Error.WriteLine($"the pairing state at {statePath} lacks {string.Join(", ", missing)} - re-pair this machine");
    return 1;
}

using var client = new VivariumClient(state);
var registry = new ToolRegistry().AddToolsFrom(new VivariumTools(client));

var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "vivarium-companion", Version = "0.1.0" },
    ServerInstructions = "Read-only view of one Vivarium team, as the paired person. Start with " +
        "vivarium_what_is_blocking(project) to see the gates in order; the reads answer everything else. " +
        "Nothing here writes: signing, approvals and passkey moments live in the app.",
    Capabilities = new ServerCapabilities { Tools = new() },
};
options.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.Ordinal);
foreach (var tool in registry.McpTools) options.ToolCollection.Add(tool);

await using var server = McpServer.Create(new StdioServerTransport(options), options);
await server.RunAsync();
return 0;
