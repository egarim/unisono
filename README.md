# Unisono

*Declare an app action once in C#. Siri on Apple, Gemini on Android, Copilot on Windows and any MCP client can call it, al unísono.*

Unisono is a .NET library plus a build-time generator for MAUI and .NET apps. You annotate ordinary C# methods as
tools, with descriptions, the same way you would for MCP. Unisono projects those tools onto each platform's native
"app tool" surface (Swift App Intents on Apple, AppFunctions on Android, App Actions on Windows) and hosts the same
tools as an MCP server inside the app. One registry, one handler per tool, several front doors.

Status: **M0 prototype, iOS**. See `docs/dotnet-siri-mcp-framework-design.md` for the design and the milestones.

## Layout

| Path | What |
|---|---|
| `src/Unisono.Core` | `ToolRegistry`: discovers `[McpServerTool]` methods, binds JSON arguments, invokes one handler per tool. |
| `src/Unisono.Mcp` | `UnisonoMcpHttpHost`: the registry as an MCP server over Streamable HTTP on `HttpListener` (no ASP.NET Core), so it runs inside a MAUI app. |
| `samples/TaskTracker` | Redth's .NET MAUI + Siri App Intents sample, modified so the Swift intents and the MCP server both call the same `TaskTools` class. |
| `samples/ConsoleHost` | Smallest host: two demo tools over MCP on localhost. |
| `scripts/mcp-smoke.sh` | Drives any Unisono endpoint with curl: initialize, tools/list, tools/call, session delete. |

## Build and run (iOS simulator)

Requirements: macOS, .NET 10 with the `ios` and `maui` workloads, Xcode 27. The installed .NET for iOS workload
may insist on a specific Xcode minor version; `-p:ValidateXcodeVersion=false` skips that check.

```bash
dotnet build samples/TaskTracker/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements="" -p:ValidateXcodeVersion=false
```

Launch the app in the simulator, then talk to it over MCP from the Mac (the simulator shares the host network):

```bash
scripts/mcp-smoke.sh http://127.0.0.1:8787/mcp create_task '{"title":"Buy oat milk","priority":"high"}'
```

Or add it to Claude Code:

```bash
claude mcp add --transport http tasktracker http://127.0.0.1:8787/mcp
```

The same `create_task` handler runs when the Shortcuts app or Siri invokes the "Create Task" intent.

Note: iOS suspends a backgrounded app's sockets, so the MCP endpoint answers only while the app is in the
foreground. The intents still run when the app is in the background, because the system wakes the app for them.
On macOS and Mac Catalyst the endpoint stays up as long as the app runs.

Verified 2026-11-10 on an iPhone 17 Pro simulator: a task created over MCP showed up in the Shortcuts "List Tasks"
intent and in the UI, and a task created through the "Create Task" intent came back from `search_tasks` over MCP.

## Desktop smoke test (no phone needed)

```bash
dotnet run --project samples/ConsoleHost -- 8788
scripts/mcp-smoke.sh http://127.0.0.1:8788/mcp add '{"a":2,"b":3}'
```

## Credits

`samples/TaskTracker` started from [Redth/Maui.Apple.PlatformFeature.Samples](https://github.com/Redth/Maui.Apple.PlatformFeature.Samples) (MIT).
Built on the [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).
