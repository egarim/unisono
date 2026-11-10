# Unisono.AppIntents

The Apple front door of [Unisono](https://github.com/egarim/unisono): connects an app's Swift App Intents to the
`ToolRegistry` through one generic JSON bridge, so Siri and Shortcuts run the same C# handlers as MCP clients.
Targets `net10.0-ios` and `net10.0-maccatalyst`; works with .NET MAUI, Uno Platform, Avalonia and plain .NET for iOS.

```bash
dotnet add package Unisono.AppIntents --prerelease
```

What the package contains:

- `UnisonoAppIntents.Attach(registry)`: the C# end of the bridge; call it once at launch.
- `native/UnisonoBridge.swift`: the Swift end (fixed `UnisonoHost` protocol, `UnisonoBridge` singleton and
  `UnisonoTools` helpers). Reference or copy it into your Swift intents framework next to your intents, entities and
  enums; every `perform()` becomes a few lines:

  ```swift
  let created = try await UnisonoTools.invoke("create_task", Args(title: taskTitle), returning: TaskDto.self)
  return .result(value: TaskEntity(created), dialog: "Created '\(created.title)'")
  ```

- `build/Unisono.AppIntents.targets`: imported automatically; after your `<XcodeProject>` item builds the Swift
  framework, it copies `Metadata.appintents` into the app bundle (flat on iOS, `Contents/Resources` on Mac Catalyst).
- `tools/new-intents-xcodeproj.py`: generates the Xcode project for your intents framework from a `Sources/` folder,
  referencing the bridge file (`--shared`) and optionally enabling Mac Catalyst.

The Swift file lives in the package at `~/.nuget/packages/unisono.appintents/<version>/native/UnisonoBridge.swift`.
Full walkthrough, the TaskTracker and Notes samples and the cross-app demos: https://github.com/egarim/unisono
