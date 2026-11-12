# Unisono Notes sample

The second demo app: short notes whose `- [ ]` lines are action items. Its five actions are reachable from Siri and
Shortcuts (App Intents) and from any MCP client, with one C# handler per action. Together with TaskTracker it tells
the cross-app story: **a note contains action items; the action items become tasks; the note is updated with what was
created.** See `docs/demo-plan.md`.

## Pieces

| Path | What |
|---|---|
| `UnisonoNotes/` | The MAUI app (iOS target). `Tools/NoteTools.cs` declares `create_note`, `list_notes`, `get_note`, `append_to_note` and `extract_action_items`; `Tools/NoteEntityQuery.cs` resolves `Note` entities; `Models/Note.cs` holds the one action-item rule the tools and the UI share. MCP listens on port 8789 (TaskTracker uses 8787). |
| `UnisonoNotes/AppIntents/` | Swift intents (Create Note, Find Notes, Extract Action Items, Append to Note, Open Note), the `Note` entity and query, and the shortcuts phrases. The bridge file is **referenced** from the package (`src/Unisono.AppIntents/native/`), never copied, and the Xcode project is written by the Unisono.AppIntents targets at build time under `obj/`. |

## Build and run

```bash
dotnet build samples/UnisonoNotes/UnisonoNotes/UnisonoNotes.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements="" -p:ValidateXcodeVersion=false
```

Then, with the app in the foreground on the simulator:

```bash
scripts/mcp-smoke.sh http://127.0.0.1:8789/mcp list_notes '{}'
```

In the Shortcuts app, "Extract Action Items" asks for a note (the picker is served by the entity query over the
bridge) and returns one text per open item, which a Shortcut can feed into TaskTracker's "Create Task".

## Mac Catalyst

The project also targets `net10.0-maccatalyst`; the Swift intents framework is built for Catalyst by the same generated
Xcode project (`SUPPORTS_MACCATALYST`). The Catalyst entitlements include `com.apple.security.network.server` so the
sandboxed app can accept MCP connections on 127.0.0.1, and `Metadata.appintents` lands in `Contents/Resources`.
