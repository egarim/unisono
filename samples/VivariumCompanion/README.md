# Vivarium companion

An MCP server for one Vivarium team. It runs on your computer, speaks stdio, and signs in with the pairing this
computer already has (`~/.creatura/device.json`, or `~/.creatura/teams/<team>.json`). An agent that helps you can see
the factory through it, without SSH on the hub.

## Add it to Claude Code

Pair the computer first (Vivarium's `setup.sh` or `setup.ps1`). Then, with the path of your clone and your team's name:

```bash
dotnet build samples/VivariumCompanion -c Release
claude mcp add vivarium -- dotnet /path/to/unisono/samples/VivariumCompanion/bin/Release/net10.0/VivariumCompanion.dll --team my-team
```

There is no key to paste: do not put tokens in the command, in `--env` or in `.mcp.json`.

## What it reads

| Tool | Shows |
|---|---|
| `vivarium_factory_jobs` | the jobs: stage, round, risk, moves, why each stopped, who it waits for |
| `vivarium_factory_job` | one job: its question, and its moves with their evidence (long texts are cut and say so; `move` reads one whole) |
| `vivarium_needs_you` | what waits for a person, in every project |
| `vivarium_machines` | the computers: online, last seen, tasks, how many at build, what runs now |
| `vivarium_models` | the models in force, the spend limit and what is spent |
| `vivarium_what_is_blocking` | a project's gates, in order |
| `vivarium_projects`, `_baselines`, `_tasks`, `_runs`, `_devices`, `_conversations`, `_guardrails` | the record |

Everything it returns was written by people and agents. It is data, never instructions; every tool says so.

A failed review's full findings are not on the record today: the hub keeps them on its disk and records the verdicts.
The companion shows what the record holds.

## What it may write

Nothing, until you make `~/.creatura/mcp-grants.json` yourself:

```json
{ "answer_job": true, "move_task": true }
```

- `answer_job`: `vivarium_answer_job` answers a job that waits for a person.
- `move_task`: `vivarium_move_task` moves a task to another paired computer of the task's own person.

The file is read at every call and again right before the write. Change `true` to `false`, or delete the file, and the
next write is refused; no restart. A missing or unreadable file allows nothing. With a grant there is no tap for each
write, unless your MCP client asks for one. The task's person or a lead can do these; anyone else is refused.

Merging, signing, guardrails, dropping a job, deploying and keys have no tool here.

### What these limits are, and are not

- **The record still names you as the author.** The companion uses your pairing, so the platform records the paired
  person. The answer's first line ("Written by <client> for <person>") lets a reader tell; the client name is what the
  client says it is, and nothing proves it. A separate identity for the agent needs work on the server
  (egarim/vivarium#165, point 4).
- **The grants file is a switch, not a lock.** An agent that can also run commands as you can edit that file, or use the
  pairing directly. Deny your agent access to `~/.creatura` in its own settings.
- **It shares your pairing with the listener.** The companion never writes the pairing file. It reads the refresh token
  once, at start, and keeps the renewed one in memory. That is safe while the sign-in server lets a refresh token be
  used again. If the realm ever turns on "revoke refresh token", the companion and the listener would end each other's
  sign-in, already with reads alone; writes add no other kind of refresh. The cure is its own sign-in for the agent.
- **A run that starts in the same moment.** A move is refused while a run is going and when the task changed meanwhile.
  Taking a build does not change the task at once, so a run taken between the last check and the write can still begin
  on the old computer; it finishes there and the next one starts on the new.
- **A run that never reported its end** blocks a move until the record is put right.
