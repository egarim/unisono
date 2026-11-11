# Changelog

All notable changes to the Unisono packages. Versions follow SemVer; previews carry a `-preview.N` suffix.

## 0.1.0-preview.2 (2026-11-12)

- New `Unisono.AppFunctions` (`net10.0-android`): `UnisonoAppFunctionService`, an `AppFunctionService` that
  dispatches Android AppFunctions (the surface Gemini and `cmd app_function` call) to the same registry;
  `GenericDocumentJson` converts parameters and results; `UnisonoAppFunctions.Attach`. The app declares the service
  in its manifest and ships `assets/app_functions.xml`, which `scripts/new-appfunctions-xml.py` writes from a running
  registry. Verified on a Pixel 7 running Android 17.
- TaskTracker sample: `net10.0-android` target (MCP host on the phone's loopback, reachable over `adb forward`),
  real-iPhone build notes, `delete_task`, `delete_completed_tasks`, a due-window filter on `list_tasks`, and Siri
  asking for the due date.

## 0.1.0-preview.1 (2026-11-10)

First public preview.

- `Unisono.Core`: `ToolRegistry` built on the MCP C# SDK's `[McpServerTool]` attributes; JSON in/out invocation;
  `IEntityQuery<T>` for entity look-ups (by id, text, suggested).
- `Unisono.Mcp`: `UnisonoMcpHttpHost`, an MCP server over Streamable HTTP on `HttpListener` (no ASP.NET Core),
  with stateful sessions and a stateless fallback.
- `Unisono.AppIntents` (iOS, Mac Catalyst): the generic Swift bridge (`native/UnisonoBridge.swift`),
  `UnisonoAppIntents.Attach`, MSBuild targets that place `Metadata.appintents` in the bundle, and the Xcode project
  generator under `tools/`.
- `Unisono.AppIntents.Binding`: the static Objective-C binding of the bridge.

Known limits: on iPhone the MCP endpoint answers only while the app is in the foreground; intent donations are not
wired yet; Android and Windows front doors are planned.
