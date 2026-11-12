# TaskTracker sample

A .NET MAUI to-do app whose six actions are reachable from **Siri and Shortcuts** (App Intents) and from **any MCP
client** (Claude Code, Claude Desktop, …), with one C# handler per action. It started as Redth's
`Maui.Apple.PlatformFeature.Samples/AppIntents` (MIT, see `LICENSE.redth` and `NOTICE.md`) and was moved onto Unisono.

## Pieces

| Path | What |
|---|---|
| `MauiAppIntentsSample/` | The MAUI app. `Tools/TaskTools.cs` declares the six tools with `[McpServerTool]` + `[Description]`; `Tools/TaskEntityQuery.cs` resolves `Task` entities. `MauiProgram.cs` builds the `ToolRegistry` and the MCP host; `Platforms/iOS/AppDelegate.cs` attaches the Siri bridge and starts MCP. |
| `MauiAppIntentsSample/AppIntents/` | Swift App Intents, entities, enums and the shortcuts provider. Every `perform()` encodes its parameters to JSON and calls `UnisonoTools.invoke`. The Unisono.AppIntents targets mirror this folder and the package's `UnisonoBridge.swift` under `obj/`, write the Xcode framework project and build it; no Xcode project or `<XcodeProject>` item is checked in. |

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
- Siri by voice needs a physical device; the simulator runs intents through the Shortcuts app. No Siri entitlement
  is needed for App Intents (that key belongs to SiriKit) and a wildcard development profile is enough: see "Real
  iPhone" below.
- On iPhone the MCP endpoint answers only while the app is in the foreground; intents also run in the background.
- Donations (`IntentDonationManager`) are paused in this version and return in M2 with generated Swift.
- `Create Task` asks for the due date when none is given ("When is it due?"), so Siri resolves "today" or "tomorrow at
  five"; a Shortcut that calls it must pass a date or it prompts. `List Tasks` takes a due window (any, today,
  tomorrow, this week, overdue) and the App Shortcut phrase "Show my tasks for today in Task Tracker" fills it.
  `Delete Task` and `Delete Completed Tasks` ask for confirmation first.
- Shortcuts stores entity references by id: keep ids stable across launches (the seed data does) or stored references
  go stale. Required enum parameters need `default:` values if an intent should be runnable from an App Shortcut tile.

## Real iPhone

Build for the device with the development identity and profile on the Mac (a team wildcard profile works), then
install and launch once with `devicectl`:

```bash
dotnet build -f net10.0-ios -r ios-arm64 -p:ValidateXcodeVersion=false \
  -p:CodesignKey="Apple Development: <name> (<id>)" -p:CodesignProvision=<profile uuid>
xcrun devicectl device install app --device <udid> bin/Debug/net10.0-ios/ios-arm64/MauiAppIntentsSample.app
xcrun devicectl device process launch --device <udid> com.companyname.mauiappintentssample
```

Verified on an iPhone 15 Pro Max (iOS 26.7): the six App Shortcuts appear in the Shortcuts app after the first
launch. Siri matches the app by its spoken name, so `Info.plist` carries `CFBundleSpokenName` and
`INAlternativeAppNames` ("Task Tracker", "Tasks"); with only the one-word display name Siri answered "I don't see an
app for that". With those keys, Siri runs the intents by voice while TaskTracker is in the foreground; from the home
screen it still did not resolve the app name right after installing (Siri's app vocabulary indexes on its own
schedule; toggling Settings → Apps → TaskTracker → Siri, or a restart, is the usual nudge). When only `Info.plist`
changes, delete `obj/.../AppManifest.plist` before rebuilding, or the .NET build keeps the old manifest.

## Mac Catalyst

The project also targets `net10.0-maccatalyst`; the Swift intents framework is built for Catalyst by the same generated
Xcode project (`SUPPORTS_MACCATALYST`). The Catalyst entitlements include `com.apple.security.network.server` so the
sandboxed app can accept MCP connections on 127.0.0.1, and `Metadata.appintents` lands in `Contents/Resources`.

## Android

The project also targets `net10.0-android`. There is no App Intents bridge there (the Apple-only project reference,
the Xcode project and the Unisono.AppIntents targets are conditioned on iOS and Catalyst), but the same registry and
the same `UnisonoMcpHttpHost` run on the phone: `Platforms/Android/MainApplication.cs` starts the server in
`OnCreate`, on the loopback address like everywhere else. From a Mac with a phone or emulator attached:

```bash
dotnet build -f net10.0-android -t:Install     # Debug uses fast deployment: deploy with the Install target, not adb install
adb forward tcp:8787 tcp:8787
python3 ../../scripts/mcp-call.py http://127.0.0.1:8787/mcp list call create_task '{"title":"From the Mac"}'
```

The MCP calls reach the app only while it is open, as on iPhone. Verified on a Pixel 7 (Android 17).

**AppFunctions (the Gemini front door).** The same registry is exposed as Android AppFunctions through
`Platforms/Android/TaskTrackerAppFunctionService.cs` (a `UnisonoAppFunctionService`), the `<service>` entry in
`Platforms/Android/AndroidManifest.xml` and `Platforms/Android/Assets/app_functions.xml`, which lists one function per
tool and is written by `scripts/new-appfunctions-xml.py` from the running app. The platform tools prove it on an
API 36+ device (the API 36 emulator image has no `cmd app_function`; a Pixel 7 on Android 17 does):

```bash
adb shell cmd app_function list-app-functions --package com.companyname.mauiappintentssample
adb shell "cmd app_function execute-app-function --package com.companyname.mauiappintentssample --function create_task --parameters '{\"title\":\"Buy oat milk\",\"priority\":\"high\"}'"
adb shell "cmd app_function execute-app-function --package com.companyname.mauiappintentssample --function complete_task --parameters '{\"id\":\"task-groceries\"}'"
```

Quote the whole command for the phone's shell as above, or JSON values with spaces lose their quotes. The result
comes back under `androidAppfunctionsReturnValue` as typed properties (a nested document for a task, arrays for
lists); the shell's converter treats a top-level `id` key as the document id, which Unisono hands to the tool's `id`
parameter. Regenerate the XML after adding tools: `python3 ../../scripts/new-appfunctions-xml.py http://127.0.0.1:8787/mcp Platforms/Android/Assets/app_functions.xml`.
