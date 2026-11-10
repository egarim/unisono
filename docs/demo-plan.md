# Unisono demo plan: Notes + TaskTracker

Decided 2026-11-10. Two small MAUI apps that share nothing but the Unisono package, used to show every way an
assistant or agent can reach an app's actions, and how actions flow between two apps.

## The pair

| | **Unisono Notes** (App A, new) | **TaskTracker** (App B, exists) |
|---|---|---|
| Role in the story | Where information arrives (meeting notes, standups) | Where work gets tracked |
| Entities | `Note` (id, title, body, created) | `Task` (id, title, priority, category, due date…) |
| Tools (= intents = MCP tools) | `create_note`, `list_notes(query?)`, `get_note(id)`, `append_to_note(id, text)`, `extract_action_items(id)` → `[string]` | `create_task`, `get_task`, `list_tasks`, `search_tasks`, `complete_task`, `set_due_date` |
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
   as tools. Needs a model: DeepSeek, OpenRouter or the LiteLLM gateway. Stretch; cut if time is short.

*Runs on:* iPhone simulator, Mac Catalyst.

### S2. Shortcut chaining between the two apps (no agent)
*Shows:* Apple's own cross-app mechanism, entities crossing app boundaries, zero code in the Shortcut beyond wiring.

A Shortcut "Note → Tasks":
1. **Notes: "Extract Action Items"** (parameter: a `Note` entity, picked from the app's `EntityQuery`).
2. **Repeat with each** item →
3. **TaskTracker: "Create Task"** (title = item, priority = Medium, category = Work).
4. **Notes: "Append to Note"** with "Created N tasks in TaskTracker on <date>".

Then open TaskTracker: the tasks are there. Open the note: the footer is there.

*Runs on:* iPhone simulator (both apps installed; intents run in the background, no socket needed).

### S3. An agent orchestrating both apps over MCP
*Shows:* the developer story: agents drive apps through their declared tools; the same tools Siri sees.

Claude Code (or Claude Desktop) connected to both endpoints. Prompt:
> "Read today's standup note in Notes and create a task in TaskTracker for each action item that is mine,
> high priority, due Friday. Then note in the standup which tasks you created."

Expected calls: `notes.list_notes` → `notes.get_note` → `tasks.create_task` × N → `notes.append_to_note`.
Both apps' UIs update as the calls land.

*Runs on:* **Mac Catalyst** (both apps in the foreground), or **two booted simulators** side by side (one app each,
the Mac reaches both ports), or an **iPad simulator in Split View**. Not on a single iPhone: the background app's
socket is suspended (observed in M0).

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

1. **M1: generic bridge.** One fixed `@objc` protocol (`invoke(tool, argumentsJSON, completion)` plus entity
   lookups) shipped as a static binding; the Swift intents only encode parameters to JSON and call it.
   TaskTracker's Swift moves onto it first (its behaviour is already verified, so regressions show immediately).
2. **Notes app** on the generic bridge: 5 intents, 1 entity, 1 shortcuts provider in Swift, all tools in C#.
3. **Mac Catalyst target** for both apps (S3). Fallback if the Swift framework will not build for Catalyst in time:
   two simulators.
4. **Shortcut for S2**, built in the Shortcuts app on the demo device/simulator; screenshots in `docs/`.
5. **Claude Code configuration for S3** (`claude mcp add` for both endpoints) and the prompt script.
6. **S1.3 in-app chat** if time allows.
7. **S4 annotations** (`.tool`, `Transferable`, skill bundle) so a device test is a signing step away.

## Open questions

- Keep TaskTracker's name and look, or restyle both apps as a matching "Unisono" pair? (Restyling costs a day.)
- Model for the optional in-app chat: DeepSeek directly, or the LiteLLM gateway (needs the mesh)?
- Recording: live demo, or a recorded walkthrough per scenario?
