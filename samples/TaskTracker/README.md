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
- Siri by voice needs a physical device; the simulator runs intents through the Shortcuts app. No Siri entitlement
  is needed for App Intents (that key belongs to SiriKit) and a wildcard development profile is enough: see "Real
  iPhone" below.
- On iPhone the MCP endpoint answers only while the app is in the foreground; intents also run in the background.
- Donations (`IntentDonationManager`) are paused in this version and return in M2 with generated Swift.
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
app for that". When only `Info.plist` changes, delete `obj/.../AppManifest.plist` before rebuilding, or the .NET
build keeps the old manifest.

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

The MCP calls reach the app only while it is open, as on iPhone. Verified on a Pixel 7 (Android 17) and on the
API 36 emulator path described in `docs/demo-plan.md`.
