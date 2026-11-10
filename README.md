# Unisono

*Declare an app action once in C#. Siri on Apple, Gemini on Android, Copilot on Windows and any MCP client can call it, al unísono.*

Unisono is a .NET library plus a build-time generator for MAUI and .NET apps. You annotate ordinary C# methods as
tools, with descriptions, the same way you would for MCP. Unisono projects those tools onto each platform's native
"app tool" surface (Swift App Intents on Apple, AppFunctions on Android, App Actions on Windows) and hosts the same
tools as an MCP server inside the app. One registry, one handler per tool, several front doors.

Status: **M1 on iOS** (generic Swift bridge; per-app Swift is intents, entities and enums only), two demo apps, and the
cross-app Shortcut "Note to Tasks" (Notes → action items → TaskTracker tasks → note updated) verified on the simulator, and
Claude Code driving both Mac Catalyst apps over MCP (read the standup note, create the tasks, write back to the note).
See `docs/demo-plan.md` and `docs/images/`. See `docs/dotnet-siri-mcp-framework-design.md` for the design and the milestones.

## Works with

Unisono is not tied to MAUI. The core packages are plain .NET, and the platform packages hook into the operating
system, not into a UI toolkit, so any .NET app that targets the platform can use them.

| App framework | iOS and Mac Catalyst (App Intents + MCP) | Android (AppFunctions + MCP, planned) | Windows (App Actions + MCP registry, planned) |
|---|---|---|---|
| **.NET MAUI** (XAML, Blazor Hybrid) | **Verified**: both sample apps | Planned | Planned |
| **Uno Platform** | Same `net10.0-ios` / `net10.0-maccatalyst` targets, not yet tested | Planned | Planned |
| **Avalonia** | Same targets, not yet tested | Planned | Planned |
| **.NET for iOS / Mac Catalyst** (UIKit, no cross-platform UI) | Same targets, not yet tested | n/a | n/a |
| **.NET for Android** (Android SDK bindings) | n/a | Planned, first in line (the SDK already binds `AppFunctionService`) | n/a |
| **WinUI 3, WPF, Windows Forms** (MSIX packaged) | n/a | n/a | Planned |
| **Console, ASP.NET Core, any .NET host** | `Unisono.Core` + `Unisono.Mcp` run anywhere .NET 10 runs (see `samples/ConsoleHost`) | | |

What each layer needs from the app:

- `Unisono.Core` and `Unisono.Mcp` need nothing platform-specific: a `netstandard2.0`/`net10.0` dependency, a registry
  built from your tool classes, and a call to start the MCP host.
- `Unisono.AppIntents` needs an app that builds for `net10.0-ios` or `net10.0-maccatalyst`, one `<XcodeProject>` item
  for the Swift intents framework, and a call to `UnisonoAppIntents.Attach(registry)` at launch. It does not touch
  the UI layer, so MAUI, Uno, Avalonia and plain UIKit apps wire it the same way.
- The Android and Windows packages will follow the same pattern: a C# adapter the app registers at startup, no
  dependency on the UI toolkit.

If you try Unisono with a framework marked "not yet tested", a short report (what worked, what did not) is welcome as
an issue.

## Layout

| Path | What |
|---|---|
| `src/Unisono.Core` | `ToolRegistry`: discovers `[McpServerTool]` methods, binds JSON arguments, invokes one handler per tool. |
| `src/Unisono.Mcp` | `UnisonoMcpHttpHost`: the registry as an MCP server over Streamable HTTP on `HttpListener` (no ASP.NET Core), so it runs inside a MAUI app. |
| `src/Unisono.AppIntents` | The Apple front door: `native/UnisonoBridge.swift` (one generic JSON bridge, copied into each app's Swift intents framework), the C# host that answers it from the registry, and MSBuild targets that put `Metadata.appintents` into the bundle. |
| `src/Unisono.AppIntents.Binding` | Static Objective-C binding of the bridge (`UnisonoHost`, `UnisonoBridge`). Written once; no per-app binding projects. |
| `samples/TaskTracker` | Redth's .NET MAUI + Siri App Intents sample, moved onto Unisono: the Swift intents only encode JSON and call the bridge; the MCP server and Siri both call the same `TaskTools` class. The app project builds its own Swift framework through an `XcodeProject` item. |
| `samples/UnisonoNotes` | Second demo app: notes whose `- [ ]` lines are action items; five tools, a `Note` entity, MCP on port 8789. With TaskTracker it tells the cross-app story (note → action items → tasks → note updated). Its Xcode project is generated. |
| `samples/ConsoleHost` | Smallest host: two demo tools over MCP on localhost. |
| `scripts/mcp-smoke.sh`, `scripts/mcp-call.py` | Drive any Unisono endpoint from the shell: initialize, tools/list, tools/call, session delete. |
| `scripts/s3-orchestrate.py`, `scripts/s3-agent-demo.sh` | The two-app scenario: scripted, and with Claude Code as the agent. |
| `scripts/new-intents-xcodeproj.py` | Generates an app's Swift intents Xcode project from its `Sources/` folder, referencing the shared bridge file; the shape the M2 code generator will drive. |

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

## Mac Catalyst (both apps side by side)

```bash
dotnet build samples/TaskTracker/MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-maccatalyst -r maccatalyst-arm64 -p:ValidateXcodeVersion=false
dotnet build samples/UnisonoNotes/UnisonoNotes/UnisonoNotes.csproj -f net10.0-maccatalyst -r maccatalyst-arm64 -p:ValidateXcodeVersion=false
open samples/TaskTracker/MauiAppIntentsSample/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/TaskTracker.app
open "samples/UnisonoNotes/UnisonoNotes/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Unisono Notes.app"
```

On the Mac both MCP endpoints stay up while the apps run. `scripts/s3-orchestrate.py` drives the note → tasks → note
flow across both without a model; `scripts/s3-agent-demo.sh` does it with Claude Code (`claude mcp add` both endpoints
first). Stop the simulator copies of the apps first: they hold the same loopback ports.

## Desktop smoke test (no phone needed)

```bash
dotnet run --project samples/ConsoleHost -- 8788
scripts/mcp-smoke.sh http://127.0.0.1:8788/mcp add '{"a":2,"b":3}'
```

## Credits

`samples/TaskTracker` started from [Redth/Maui.Apple.PlatformFeature.Samples](https://github.com/Redth/Maui.Apple.PlatformFeature.Samples) (MIT).
Built on the [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

## License

MIT, see `LICENSE`. The TaskTracker sample keeps its original MIT notice in `samples/TaskTracker/LICENSE.redth`.
