# Unisono demo plan: Notes + TaskTracker

Decided 2026-11-10. Two small MAUI apps that share nothing but the Unisono package, used to show every way an
assistant or agent can reach an app's actions, and how actions flow between two apps.

## The pair

| | **Unisono Notes** (App A, new) | **TaskTracker** (App B, exists) |
|---|---|---|
| Role in the story | Where information arrives (meeting notes, standups) | Where work gets tracked |
| Entities | `Note` (id, title, body, created) | `Task` (id, title, priority, category, due date…) |
| Tools (= intents = MCP tools) | `create_note`, `list_notes(query?)`, `get_note(id)`, `append_to_note(id, text)`, `extract_action_items(id)` → `[string]` | `create_task`, `get_task`, `list_tasks(category?, priority?, showCompleted?, due?)`, `search_tasks`, `complete_task`, `set_due_date`, `delete_task`, `delete_completed_tasks` |
| MCP endpoint (Mac / simulator) | `http://127.0.0.1:8789/mcp` | `http://127.0.0.1:8787/mcp` |

`extract_action_items` is deterministic (lines starting with `- [ ]`, `TODO:` or `Action:`), so the Shortcut-only
flow works without any model; agents (Claude, Siri AI) can read the note body and extract items themselves.

The handoff is obvious in every scenario: **a note contains action items; the action items become tasks; the note is
updated with what was created.**

## Scenarios, cheapest first

### S1. One app, every front door (TaskTracker alone)
*Shows:* one C# method, several callers. Already working for two of the three callers (M0).

1. **Shortcuts / Siri:** run "Create Task" → "Call the dentist" appears in the app.
2. **Claude Code:** `claude mcp add --transport http tasktracker http://127.0.0.1:8787/mcp`, then
   "What is urgent this week?" (`list_tasks`) and "Mark the login bug done" (`search_tasks` + `complete_task`);
   the list updates live in the app.
3. **In-app chat (optional):** a chat page using `Microsoft.Extensions.AI` with the same `registry.Functions`
   as tools. Needs a model behind an OpenAI-compatible endpoint. Stretch; cut if time is short.

*Runs on:* iPhone simulator, Mac Catalyst.

### S2. Shortcut chaining between the two apps (no agent)
*Shows:* Apple's own cross-app mechanism, entities crossing app boundaries, zero code in the Shortcut beyond wiring.

**Built and verified 2026-11-10 on the iPhone 17 Pro simulator** (shortcut "Note to Tasks", see `docs/images/`):
1. **Unisono Notes: Extract Action Items** with Note = "Standup, Monday" (picked from the entity query's list).
2. **Repeat with Each** over the result (Shortcuts wired the input automatically).
3. inside the loop, **TaskTracker: Create Task** with Title = `Repeat Item`, Priority = Medium, Category = Work.
4. **Unisono Notes: Append to Note**, Note = "Standup, Monday", Text = "Created tasks in TaskTracker via the Note to
   Tasks shortcut".

Run: "Found 3 action items in 'Standup, Monday'" → "Created 'Send the Q4 report to finance' with Medium priority in
Work" (×3) → "Added it to 'Standup, Monday'". Checked over MCP afterwards: `list_tasks {category: work}` on
TaskTracker lists the three new tasks with the action-item titles; `list_notes {query: Standup}` on Notes shows the
appended line and a new `updatedAt`. Both intents ran while their apps were in the background, which is why the
Shortcut path needs no socket.

Editor notes for the demo: a newly added action lands after "End Repeat"; long-press and drag it into the loop. Set
Priority and Category on Create Task, or the loop prompts for them on every item. "Show When Run" on Create Task
makes each iteration pause on a result card; switch it off for a smooth run.

Two findings from the recording session (2026-11-10), both fixed in the samples:
- A Shortcut stores entity references by **id**. The seed data used random ids, so after relaunching Notes the
  stored "Standup, Monday" reference went stale and the Shortcut asked for the note again. Seed data now has stable
  ids (`note-standup-monday`, `task-login-bug`, …); real apps have this for free with persisted data.
- Running **Create Task from its App Shortcut tile** failed after the title prompt with "This action could not be
  run with the current user interface": required enum parameters without defaults cannot be asked for from a tile.
  The Swift parameters now default to Medium / Personal, so the tile asks only for the title; inside a Shortcut the
  values can still be set explicitly. Complete Task from the tile works and shows the entity picker.

*Runs on:* iPhone simulator or device with both apps installed.

### S3. An agent orchestrating both apps over MCP
*Shows:* the developer story: agents drive apps through their declared tools; the same tools Siri sees.

**Mac Catalyst builds done and verified 2026-11-10.** Both apps build with `-f net10.0-maccatalyst -r maccatalyst-arm64`
(plus `-p:ValidateXcodeVersion=false` on this machine), run side by side on the Mac, and keep their MCP endpoints up
(8787 TaskTracker, 8789 Notes) for as long as they run. What Catalyst needed: the `com.apple.security.network.server`
entitlement (the sandboxed app must accept loopback connections), `Metadata.appintents` copied under
`Contents/Resources` instead of the bundle root (Unisono's targets now do this), `SUPPORTS_MACCATALYST` in the generated
Xcode projects, and stopping the simulator copies first because they hold the same loopback ports.

Two ways to run the scenario:

- **`scripts/s3-orchestrate.py`** (no model): the deterministic version of what an agent does. Verified: it read
  "Standup, Monday" over the Notes endpoint, created three tasks (high, work, due 2026-11-14) over the TaskTracker
  endpoint, and appended "Created in TaskTracker …" to the note. It skips items that already exist as tasks.
- **`scripts/s3-agent-demo.sh`** (Claude Code): after `claude mcp add --transport http tasktracker …` and `… notes …`
  (stored in Claude Code's local project config), it runs the prompt below with `claude -p` and the two MCP servers
  allowed. **Ran successfully 2026-11-10** against the two Catalyst apps (7 turns, 19 s): the agent called
  `notes.list_notes` ("Standup, Monday"), `tasktracker.list_tasks` (all, including completed), then
  `tasktracker.create_task` twice (high, work, due 2026-11-14, each with a note pointing back to the standup) and
  `notes.append_to_note`. It skipped the dentist item on its own because the seeded task "Schedule dentist appointment"
  already covers it, and said so in the appended line: exactly the judgment a Shortcut cannot make. The CLI must be
  signed in (`claude`, then `/login`); when run from inside a Claude Code desktop session, clear that session's
  `ANTHROPIC_BASE_URL`/`CLAUDECODE` variables first or the child run tries to use the host's auth.

Prompt:
> "Find the note titled 'Standup, Monday' in notes, read its full body, and create one task in tasktracker for each
> open action item that does not already exist, high priority, category work, due 2026-11-14. Then append one line to
> that note listing the tasks you created."

Expected calls: `notes.list_notes` → `notes.get_note` → `tasktracker.list_tasks` → `tasktracker.create_task` × N →
`notes.append_to_note`. Both apps' windows update as the calls land.

*Runs on:* **Mac Catalyst** (verified), or two booted simulators side by side, or an iPad simulator in Split View.
Not on a single iPhone: the background app's socket is suspended (observed in M0).

### S4. Siri AI across both apps (stretch, device only)
*Shows:* the spring 2027 story: Siri's model picks tools from both apps and follows an App Skill.

- Mark the intents `@AppIntent(.tool)`; give every intent and parameter a `title` and `description` written for a
  model; make `Note` and `Task` entities `Transferable`.
- Add `Skills.skillsbundle/Skills/notes-to-tasks/SKILL.md` to Notes describing the handoff.
- Ask Siri: "Turn my standup note into tasks."

*Runs on:* a physical device on the 27.3 developer beta, Apple Intelligence region and language, development
signing. Not in the simulator. We design S1–S3 so that S4 needs no new tools, only annotations.

### S5. App-to-app without Siri (one slide, no build)
App A's in-app agent calling App B's MCP endpoint works on the Mac and fails on a single iPhone for the same
suspension reason. State the limit; point at S2 (Shortcuts) and S4 (Siri) as the on-phone paths.

## Build order

The Notes app needs a second Swift intents layer. Hand-writing it Redth-style (one `@objc` method per intent) would
be thrown away in M1, so the demo becomes the vehicle for M1:

1. **M1: generic bridge.** Done 2026-11-10. One fixed `@objc` protocol (`invoke(tool, argumentsJSON, completion)`
   plus entity lookups) behind a static binding; the Swift intents only encode parameters to JSON and call it.
   TaskTracker runs on it: List Tasks, Create Task and Complete Task (entity picker included) verified in Shortcuts,
   MCP verified with curl, and the per-app binding project is gone (the app builds its own `XcodeProject`; since
   2026-11-12 the Unisono.AppIntents targets write that project themselves from the app's `AppIntents/` folder).
2. **Notes app** on the generic bridge: 5 intents, 1 entity, 1 shortcuts provider in Swift, all tools in C#.
   Done 2026-11-10 (`samples/UnisonoNotes`): MCP on 8789 lists five tools, `extract_action_items` returns the three
   open items of the seeded standup note and `append_to_note` updates it; in Shortcuts, "Action Items" shows the note
   picker from the entity query and reports "Found 3 action items in 'Standup, Monday'". Both apps are installed on
   the same simulator, so S2 (the Shortcut chain) can be built next. The Xcode project is generated by
   the Unisono.AppIntents targets (an inline MSBuild task) and references the shared bridge file instead of copying it.
3. **Mac Catalyst target** for both apps (S3). Done 2026-11-10; see S3 above. Both the scripted orchestration and
   the Claude Code agent run are verified.
4. **Shortcut for S2**, built in the Shortcuts app on the demo device/simulator; screenshots in `docs/`.
   Done 2026-11-10 (see S2 above).
5. **Claude Code configuration for S3** (`claude mcp add` for both endpoints) and the prompt script.
6. **S1.3 in-app chat** if time allows.
7. **S4 annotations** (`.tool`, `Transferable`, skill bundle) so a device test is a signing step away.

## Open questions

- Keep TaskTracker's name and look, or restyle both apps as a matching "Unisono" pair? (Restyling costs a day.)
- Model for the optional in-app chat: which provider, and configured how (never committed; read from the environment)?
- Recording: live demo, or a recorded walkthrough per scenario?
