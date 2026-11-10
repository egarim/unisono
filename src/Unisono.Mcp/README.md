# Unisono.Mcp

Hosts a [Unisono](https://github.com/egarim/unisono) `ToolRegistry` as an MCP server over Streamable HTTP, on
`System.Net.HttpListener`, so it runs inside a .NET MAUI, Uno, Avalonia or desktop app without ASP.NET Core.
Claude Code, Claude Desktop and any other MCP client connect to `http://127.0.0.1:<port>/mcp` and call the same
tools Siri and Shortcuts see.

```bash
dotnet add package Unisono.Mcp --prerelease
```

```csharp
var host = new UnisonoMcpHttpHost(registry, new UnisonoMcpHttpOptions
{
    Port = 8787,
    ServerName = "tasktracker",
    Instructions = "Use list_tasks to find a task id before completing it.",
});
host.Start();   // stop with await host.DisposeAsync()
```

Stateful sessions (`Mcp-Session-Id`) are supported, and a POST without a session is served statelessly. On iPhone the
endpoint answers only while the app is in the foreground; on macOS and Mac Catalyst it stays up while the app runs.
Documentation and samples: https://github.com/egarim/unisono
