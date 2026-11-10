# Unisono: declare an app action once in C#, and every assistant can call it

*Draft for the launch of Unisono, a .NET library for MAUI and .NET apps. Written 2026-11-10, when milestones M0 and M1
were working on iOS and the two-app demo had just run.*

## The problem

Every assistant now wants to call your app, not just open it. Siri does it through App Intents. Gemini on Android does
it through AppFunctions. Copilot on Windows does it through App Actions. Claude, Cursor and the other agents do it
through MCP, the Model Context Protocol. Four front doors, and behind each one the same question: *what can this app
do, and how do I ask?*

The platforms have converged on one shape. The app declares typed, described functions in static metadata; the
operating system indexes them; a model picks one and fills in the parameters. Only the declaration format and the
dispatch mechanism differ. Apple wants Swift structs with `@Parameter` properties and compile-time metadata. Android
wants an XML asset and a service. Windows wants JSON in the package and a provider class. MCP wants a JSON schema over
HTTP or stdio.

If you write .NET apps, that is three native layers plus an MCP server, each describing the same six or seven actions
in its own words. "Create a task" exists four times. The descriptions drift. One day Siri says the priority is
"urgent" and the MCP tool only knows "high". Nobody planned that; it is just what happens when one idea lives in four
files.

## One definition

Unisono starts from the MCP side, because MCP already has the right vocabulary for this: a tool is a method with a
name, a description and typed parameters, each with a description of its own. The MCP C# SDK expresses that with two
attributes on an ordinary method. So that is the whole declaration:

```csharp
[McpServerToolType]
public sealed class TaskTools(ITaskService tasks)
{
    [McpServerTool(Name = "create_task", Title = "Create Task")]
    [Description("Creates a new task in TaskTracker and returns it.")]
    public TaskDto CreateTask(                       // abridged: notes and estimated minutes left out
        [Description("Short title of the task")] string title,
        [Description("Priority: low, medium, high or urgent")] TaskPriorityLevel priority = TaskPriorityLevel.Medium,
        [Description("Category: work, personal, shopping, health or fitness")] TaskCategoryType category = TaskCategoryType.Personal,
        [Description("Due date and time, ISO 8601")] DateTime? dueDate = null,
        ...)
        => TaskDto.From(tasks.Create(title, priority, category, dueDate, ...));
}
```

A registry discovers these methods at startup, binds JSON arguments to them and invokes one handler per tool. Everything
else is a *projection* of that registry:

- **MCP.** The registry is served as an MCP server inside the app, over Streamable HTTP on a plain `HttpListener`,
  so it runs inside a MAUI app with no ASP.NET Core. Any MCP client on the machine can list the tools and call them.
- **Siri and Shortcuts.** A thin Swift layer of App Intents, one per tool, whose only job is to encode its parameters
  to JSON and call the registry through a bridge.
- **An in-app agent.** The same tools are `AIFunction`s, so a chat page built on `Microsoft.Extensions.AI` gets them
  for free.

The descriptions matter more than they look. Siri's model and an MCP client both choose tools and fill parameters from
them. Writing them once, in C#, next to the code that runs, is the point.

### The bridge is generic

The first version of the Apple side bridged each intent with its own Objective-C method, which meant a binding project
per app. Unisono replaced that with one fixed protocol, written once and compiled into every app's intents framework:

```swift
@objc(UnisonoHost) public protocol UnisonoHost: AnyObject {
    func invoke(_ tool: String, argumentsJSON: String, completion: @escaping (String?, String?) -> Void)
    func entities(_ type: String, ids: [String], completion: @escaping (String?, String?) -> Void)
    func searchEntities(_ type: String, text: String, completion: @escaping (String?, String?) -> Void)
    func suggestedEntities(_ type: String, completion: @escaping (String?, String?) -> Void)
}
```

C# implements it once. Each intent's `perform()` is three lines: build an `Encodable` struct that mirrors the C# tool's
parameters, call `invoke` with the tool's name, decode the result.

```swift
let args = Args(title: taskTitle, priority: priority, category: category, dueDate: dueDate)
let created = try await UnisonoTools.invoke("create_task", args, returning: TaskDto.self)
return .result(value: TaskEntity(created), dialog: "Created '\(created.title)'")
```

Because the Objective-C names never change, the binding is static and ships with the library. No per-app binding, no
native reference to maintain. The app's own project builds the Swift framework through an `XcodeProject` item, the
framework is linked automatically, and a build target copies the intents metadata into the bundle.

The same JSON contract (invoke a tool by name, look up entities by id, by text, or suggested) is what the Android and
Windows adapters will implement. The Swift file is the first adapter of the bridge, not the bridge itself.

## Two front doors, proven

The proof app is TaskTracker, a .NET MAUI to-do app that started as a public MAUI App Intents sample and was moved onto
Unisono. Its six actions (create, get, list, search, complete, set due date) are one C# class. On 2026-11-10, on an
iPhone 17 Pro simulator:

1. A task created over MCP, from a shell script on the Mac, showed up in the Shortcuts app's "List Tasks" intent and
   in the app's own list.
2. A task created through the "Create Task" intent in Shortcuts came back from `search_tasks` over MCP.

Same method, two callers, in both directions. The Shortcuts app also shows Create Task, Open Task, Complete Task and
List Tasks as App Shortcuts, with the entity picker working for Complete Task. Adding the app to Claude Code is one
command, and "what is urgent this week?" becomes a `list_tasks` call that the app answers.

Two honest limits, both platform facts rather than Unisono's:

- On iPhone, the MCP endpoint answers only while the app is in the foreground, because iOS suspends a backgrounded
  app's sockets. The intents still run in the background, because the system wakes the app for them. On macOS and Mac
  Catalyst the endpoint stays up as long as the app runs.
- Siri by voice needs a physical device, the Siri entitlement and a real provisioning profile. The simulator runs
  intents through the Shortcuts app, which is enough to prove the plumbing.

## Why the name

*Unísono* is Spanish for "in unison". Siri, Gemini, Copilot and the agents all call the same action, al unísono. The
identifier is the plain ASCII `Unisono`; in prose it keeps its accent.

## Install

The packages are on NuGet as a prerelease while the API settles:

```bash
dotnet add package Unisono.Core --prerelease         # the tool registry, any .NET 10 app
dotnet add package Unisono.Mcp --prerelease          # the in-app MCP server
dotnet add package Unisono.AppIntents --prerelease   # the Siri and Shortcuts bridge, iOS and Mac Catalyst
```

`Unisono.AppIntents` brings the static binding with it; nobody installs that one directly.

## What it is not

It is not a Swift or Kotlin interop layer, and it does not try to make Siri or Gemini run C#. The native layers stay
thin and, from the next milestone, generated. All logic stays in .NET.

## What comes next

**The two-app demo, already running.** A second small app, Notes, where a meeting note holds action items, next to
TaskTracker, where work is tracked. Notes has five tools (create, find, get, append, extract action items), one `Note`
entity and its own MCP endpoint, all on the same generic bridge; its Swift intents are the only per-app Swift. Same
handoff in every scenario: the note's action items become tasks, and the note is updated with what was created.

- *A Shortcut, no agent.* "Note to Tasks": Extract Action Items from the standup note, Repeat with Each, Create Task
  in TaskTracker, Append to Note. Built in the Shortcuts app and verified on the simulator on 2026-11-10: three action
  items became three tasks, and the note got its footer. Both intents ran with their apps in the background, which is
  why this path needs no socket.
- *An agent over MCP.* Both apps built for Mac Catalyst, side by side, each with its MCP endpoint up. A script that
  does what an agent does (read the note, create the tasks, append the line) is verified; the same flow driven by an
  agent from one prompt is a configuration step away.
- *Siri's own model, on a device.* Still ahead: mark the intents as tools, describe them for a model, add an App Skill
  for the handoff. The demo is designed so that this scenario needs no new tools, only annotations.

**Generated Swift.** M1 proved the generic bridge with hand-written intents. M2 generates the Swift intents, entities
and enums from the C# declarations at build time, so the per-app Swift goes to zero.

**Android.** AppFunctions metadata is plain XML and the dispatch is a plain Android service, and the .NET Android
bindings already contain the platform API. The generator writes the XML and manifest entries; a C# service subclass
turns the request into JSON and calls the same registry. No Kotlin, no KSP, no Gradle. Android is the natural second
platform precisely because it needs no native toolchain step.

**Windows.** App Actions on Windows are JSON definitions in the package plus a provider class; the generator writes
both. And Windows' own MCP registry can register the in-app MCP host, so on Windows the two front doors are the OS's
own pair.

**Elsewhere.** Alexa+ is an MCP client to a hosted server, so the same registry in a server build covers it. The tool
definitions do not change; only the hosting does.

The repository is public at https://github.com/egarim/unisono, with the TaskTracker and Notes samples, the generic
bridge, the in-app MCP host and a smoke script that drives any Unisono endpoint from a shell. If you build .NET apps and want them to answer when an assistant
calls, this is for you. Tell me which front door you need first.
