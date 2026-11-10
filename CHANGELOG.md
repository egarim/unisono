# Changelog

All notable changes to the Unisono packages. Versions follow SemVer; previews carry a `-preview.N` suffix.

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
