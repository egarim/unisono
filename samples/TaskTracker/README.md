# TaskTracker sample

A .NET MAUI to-do app whose six actions are reachable from **Siri and Shortcuts** (App Intents) and from **any MCP
client** (Claude Code, Claude Desktop, …), with one C# handler per action. It started as Redth's
`Maui.Apple.PlatformFeature.Samples/AppIntents` (MIT, see `LICENSE.redth` and `NOTICE.md`) and was moved onto Unisono.

## Pieces

| Path | What |
|---|---|
| `MauiAppIntentsSample/` | The MAUI app. `Tools/TaskTools.cs` declares the six tools with `[McpServerTool]` + `[Description]`; `Tools/TaskEntityQuery.cs` resolves `Task` entities. `MauiProgram.cs` builds the `ToolRegistry` and the MCP host; `Platforms/iOS/AppDelegate.cs` attaches the Siri bridge and starts MCP. |
| `MauiAppIntentsSample.AppIntents/` | Swift framework with the App Intents, entities, enums and the shortcuts provider. Every `perform()` encodes its parameters to JSON and calls `UnisonoTools.invoke`; `Bridge/UnisonoBridge.swift` is a copy of `src/Unisono.AppIntents/native/UnisonoBridge.swift`. The app project builds it through an `<XcodeProject>` item. |

There is no per-app binding project: the C# side of the bridge is the static `Unisono.AppIntents.Binding`.

## Build and run

```bash
dotnet build MauiAppIntentsSample/MauiAppIntentsSample.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements="" -p:ValidateXcodeVersion=false
```

Install the `.app` from `bin/Debug/net10.0-ios/iossimulator-arm64/` on a simulator (or let your IDE do it), open it once,
then:

- **Shortcuts app:** TaskTracker shows Create Task, Open Task, Complete Task and List Tasks as App Shortcuts; all six
  intents are available as actions.
- **MCP:** `../../scripts/mcp-smoke.sh http://127.0.0.1:8787/mcp list_tasks '{}'`, or
  `claude mcp add --transport http tasktracker http://127.0.0.1:8787/mcp`.

## Things that bite

- Intents are discovered from `Metadata.appintents` inside the app bundle, produced by Xcode when the Swift framework
  builds. Unisono's targets copy it; if it is missing, intents fail silently.
- Siri voice needs a physical device, the `com.apple.developer.siri` entitlement and a non-wildcard provisioning
  profile. The simulator runs intents through the Shortcuts app.
- On iPhone the MCP endpoint answers only while the app is in the foreground; intents also run in the background.
- Donations (`IntentDonationManager`) are paused in this version and return in M2 with generated Swift.
