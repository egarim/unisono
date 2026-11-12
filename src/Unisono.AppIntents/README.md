# Unisono.AppIntents

The Apple front door of [Unisono](https://github.com/egarim/unisono): turns the tools in a `ToolRegistry` into Swift
App Intents, so Siri and Shortcuts run the same C# handlers as MCP clients. Targets `net10.0-ios` and
`net10.0-maccatalyst`; works with .NET MAUI, Uno Platform, Avalonia and plain .NET for iOS.

```bash
dotnet add package Unisono.AppIntents --prerelease
```

One thing an app does: `UnisonoAppIntents.Attach(registry)` once at launch. Everything else happens at build time.
After the C# compile, the package reads the app assembly and writes the Swift for every `[McpServerTool]`:

- an `AppIntent` per tool (parameters typed from the C# signature, defaults kept, required values asked for by Siri,
  destructive tools confirmed first, the tool's `[Description]` as the intent description);
- an `AppEnum` per C# enum the tools use, a `Codable` struct per record they return;
- an `AppEntity` plus `EntityStringQuery` per `IEntityQuery<T>` the app registers, so a string parameter named `id`
  (or `taskId`) becomes a picker of that entity;
- an `AppShortcutsProvider` with the phrase "<tool title> in <app name>" for each intent (iOS allows ten).

Then it mirrors that Swift and the package's `native/UnisonoBridge.swift` under `obj/`, writes an Xcode framework
project for them, hands it to the .NET SDK's `<XcodeProject>` build, and copies `Metadata.appintents` into the app
bundle, which is where iOS discovers App Intents.

To customise, put Swift in an `AppIntents/` folder next to the csproj: a struct, enum or `AppShortcutsProvider` with the
same name as a generated one replaces it (custom phrases, dialogs, a due-date prompt, an intent that opens the app);
anything else is added. Every `perform()` is a few lines against the bridge:

```swift
let created = try await UnisonoTools.invoke("create_task", Args(title: taskTitle), returning: TaskDto.self)
return .result(value: TaskEntity(created), dialog: "Created '\(created.title)'")
```

Knobs (MSBuild properties): `UnisonoGenerateSwift=false` keeps the Xcode project but not the generated Swift;
`UnisonoAppIntentsDir` (default `AppIntents`); `UnisonoIntentsModuleName` (default `<AssemblyName>Intents`);
`UnisonoIntentsBundleId` (default `<ApplicationId>.intents`); `UnisonoIntentsDeploymentTarget` (default the app's
`SupportedOSPlatformVersion`); `UnisonoGenerateXcodeProject=false` to opt out of all of it. An app that declares its
own `<XcodeProject>` item is left alone. `[UnisonoEntity("Task")]` on a query class or a string parameter names the
entity when the convention (DTO name without "Dto"; parameter "id" or "taskId") is not enough.

Full walkthrough, the TaskTracker and Notes samples and the cross-app demos: https://github.com/egarim/unisono
