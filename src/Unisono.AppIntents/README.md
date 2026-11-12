# Unisono.AppIntents

The Apple front door of [Unisono](https://github.com/egarim/unisono): connects an app's Swift App Intents to the
`ToolRegistry` through one generic JSON bridge, so Siri and Shortcuts run the same C# handlers as MCP clients.
Targets `net10.0-ios` and `net10.0-maccatalyst`; works with .NET MAUI, Uno Platform, Avalonia and plain .NET for iOS.

```bash
dotnet add package Unisono.AppIntents --prerelease
```

Two things an app does:

1. `UnisonoAppIntents.Attach(registry)` once at launch (the C# end of the bridge).
2. Put its Swift intents, entities, enums and `AppShortcutsProvider` in an `AppIntents/` folder next to the csproj.
   Every `perform()` is a few lines against the bridge:

   ```swift
   let created = try await UnisonoTools.invoke("create_task", Args(title: taskTitle), returning: TaskDto.self)
   return .result(value: TaskEntity(created), dialog: "Created '\(created.title)'")
   ```

The package's MSBuild targets do the rest on every build: they mirror that folder and the package's
`native/UnisonoBridge.swift` under `obj/`, write an Xcode framework project for them (deterministic, Mac Catalyst
enabled), hand it to the .NET SDK's `<XcodeProject>` build, and copy the resulting `Metadata.appintents` into the app
bundle (flat on iOS, `Contents/Resources` on Mac Catalyst), which is where iOS discovers App Intents.

Knobs (MSBuild properties): `UnisonoAppIntentsDir` (default `AppIntents` next to the csproj), `UnisonoIntentsModuleName`
(default `<AssemblyName>Intents`), `UnisonoIntentsBundleId` (default `<ApplicationId>.intents`),
`UnisonoIntentsDeploymentTarget` (default the app's `SupportedOSPlatformVersion`), `UnisonoGenerateXcodeProject=false`
to opt out. An app that declares its own `<XcodeProject>` item is left alone.

Full walkthrough, the TaskTracker and Notes samples and the cross-app demos: https://github.com/egarim/unisono
