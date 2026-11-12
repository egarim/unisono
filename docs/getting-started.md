# Getting started: add Unisono to your app, step by step

This guide takes a .NET app from nothing to "Siri, Shortcuts, Android AppFunctions and any MCP client call the same
C# method". The running example is a trimmed copy of the TaskTracker sample (`samples/TaskTracker`), so every step has
a complete, working version in the repo to compare against.

## First: is Unisono a new attribute?

No. You mark your methods with the **same attributes the official MCP C# SDK uses**: `[McpServerToolType]` on the
class, `[McpServerTool]` on each method and `[Description]` on the method and its parameters. If you already wrote MCP
tools in C#, those classes work with Unisono unchanged. Unisono has one optional attribute of its own,
`[UnisonoEntity]`, for the rare case in step 3 where it cannot tell which kind of object an id refers to.

What Unisono adds is everything around those methods:

| You write | Unisono does |
|---|---|
| C# methods with `[McpServerTool]` and `[Description]` (once) | `ToolRegistry` reads them, builds the JSON schemas and runs them from JSON |
| One line to start the MCP host | `Unisono.Mcp` serves the tools to Claude, Copilot and any MCP client, inside the app |
| One line to attach the Apple bridge | `Unisono.AppIntents` writes and builds a Swift App Intent per tool and connects it to the C# method |
| One line to attach the Android bridge, a service class and a manifest entry | `Unisono.AppFunctions` runs every AppFunction call through the C# method |
| Optionally, an `IEntityQuery<T>` for Siri's pickers | The bridge answers Siri's "which task?" lookups from C# |

The C# method is the only place the action's logic lives. Each assistant reaches it through its own front door.

On Apple, the package writes the Swift during the build, so the app has no Swift unless it wants to customise. On
Android, you still generate the function list XML with a script (step 6).

## Step 1. Install the packages

Add only what your targets need. All of them are prerelease for now.

```bash
dotnet add package Unisono.Core --prerelease          # every app: the tool registry
dotnet add package Unisono.Mcp --prerelease           # the in-app MCP server (any platform)
dotnet add package Unisono.AppIntents --prerelease    # Siri and Shortcuts: net10.0-ios and net10.0-maccatalyst
dotnet add package Unisono.AppFunctions --prerelease  # Android AppFunctions: net10.0-android (API 36+ devices)
```

`Unisono.Core` brings in the MCP C# SDK (`ModelContextProtocol.Core`), which is where the attributes come from. In a
multi-target MAUI project, put the platform packages under a condition so each one only reaches its platform:

```xml
<PropertyGroup>
  <_Platform>$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)'))</_Platform>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Unisono.Core" Version="0.1.0-preview.2" />
  <PackageReference Include="Unisono.Mcp" Version="0.1.0-preview.2" />
  <PackageReference Include="Unisono.AppIntents" Version="0.1.0-preview.2"
                    Condition="'$(_Platform)' == 'ios' Or '$(_Platform)' == 'maccatalyst'" />
  <PackageReference Include="Unisono.AppFunctions" Version="0.1.0-preview.2"
                    Condition="'$(_Platform)' == 'android'" />
</ItemGroup>
```

## Step 2. Write the actions as MCP tools

An ordinary class, ordinary methods, the MCP SDK's attributes. Inject whatever services the methods need.

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public sealed class TaskTools(ITaskService tasks)
{
    [McpServerTool(Name = "create_task", Title = "Create Task", Destructive = false)]
    [Description("Creates a new task in TaskTracker and returns it.")]
    public TaskDto CreateTask(
        [Description("Short title of the task")] string title,
        [Description("Priority: low, medium, high or urgent")] TaskPriorityLevel priority = TaskPriorityLevel.Medium,
        [Description("Due date and time, ISO 8601")] DateTime? dueDate = null)
        => TaskDto.From(tasks.Create(title, priority, dueDate));

    [McpServerTool(Name = "complete_task", Title = "Complete Task", Idempotent = true, Destructive = false)]
    [Description("Marks a task as completed. Returns false if the task does not exist.")]
    public bool CompleteTask([Description("The task id")] string id) => tasks.Complete(id);
}

// What every front door receives as JSON. TaskDto.From maps the app's own model to it.
public sealed record TaskDto(string Id, string Title, TaskPriorityLevel Priority, DateTime? DueDate, bool IsCompleted)
{
    public static TaskDto From(TaskItem t) => new(t.Id, t.Title, t.Priority, t.DueDate, t.IsCompleted);
}
```

What matters here:

- **`Name`** is the tool name every front door uses: the MCP tool name, the name the Swift intent calls, and the
  Android function id. Without it, the method name is used.
- **`Title`** is also the action's name in Siri and Shortcuts and the start of its default Siri phrase.
- **Descriptions are the interface.** MCP clients and assistant models pick a tool and fill its parameters from them.
- **`ReadOnly`, `Destructive`, `Idempotent` and `Title`** go out to MCP clients as tool annotations, exactly as with
  the MCP SDK alone. On Apple, `Destructive = true` also makes Siri ask before running the action.
- **The JSON is camelCase** and enums travel as camelCase strings (`"high"`), dates as ISO 8601. The generated Swift
  and the Android service read the same JSON.
- `[McpServerToolType]` is not required by Unisono, which registers an instance you hand it. Keeping it lets the
  same class also work with the MCP SDK's own assembly scanning.

## Step 3. Build one registry

Register the tool class and build a `ToolRegistry` from it. In a MAUI app this goes in `MauiProgram.cs`:

```csharp
using Unisono;
using Unisono.Mcp;

builder.Services.AddSingleton<ITaskService, TaskService>();
builder.Services.AddSingleton<TaskTools>();
builder.Services.AddSingleton(sp => new ToolRegistry()
    .AddToolsFrom(sp.GetRequiredService<TaskTools>())
    .AddEntityQuery("Task", new TaskEntityQuery(sp.GetRequiredService<ITaskService>())));
```

`AddToolsFrom` can be called once per tool class. `AddEntityQuery` is optional: add it when Siri or Shortcuts should be
able to pick one of your objects ("complete *the dentist task*"). It takes an `IEntityQuery<T>` with three methods:

```csharp
public sealed class TaskEntityQuery(ITaskService tasks) : IEntityQuery<TaskDto>
{
    public ValueTask<IReadOnlyList<TaskDto>> ByIdsAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
        => new(ids.Select(tasks.GetById).Where(t => t is not null).Select(t => TaskDto.From(t!)).ToList());

    public ValueTask<IReadOnlyList<TaskDto>> SearchAsync(string text, CancellationToken ct = default)
        => new(tasks.Search(text).Select(TaskDto.From).ToList());

    public ValueTask<IReadOnlyList<TaskDto>> SuggestedAsync(CancellationToken ct = default)
        => new(tasks.GetAll().Select(TaskDto.From).ToList());
}
```

Name the entity after its record without the `Dto`, `Model` or `Record` suffix: `"Task"` for `TaskDto`. The Apple
build finds the query class by itself and uses that name. A string parameter named `taskId` then becomes a picker of
tasks, and so does one named `id` while the app has a single entity type. When a name does not fit that rule, add the
optional hint to the query class or to the parameter:

```csharp
[UnisonoEntity("Task")]
public sealed class TaskLookup(ITaskService tasks) : IEntityQuery<TaskItemView> { /* ... */ }

public bool CompleteTask([UnisonoEntity("Task"), Description("The task")] string ticket) => tasks.Complete(ticket);
```

The registry is the hub. Steps 4 to 6 each connect one front door to it; skip the ones your app does not target.

## Step 4. Front door: MCP (any platform)

Create the host next to the registry and start it once the app is up.

```csharp
builder.Services.AddSingleton(sp => new UnisonoMcpHttpHost(
    sp.GetRequiredService<ToolRegistry>(),
    new UnisonoMcpHttpOptions
    {
        Port = 8787,
        ServerName = "tasktracker",
        Instructions = "Use list_tasks or search_tasks to find a task id before completing it.",
    }));

// at launch (AppDelegate.FinishedLaunching on Apple, MainApplication.OnCreate on Android):
IPlatformApplication.Current?.Services.GetService<UnisonoMcpHttpHost>()?.Start();
```

The server listens on `http://127.0.0.1:8787/mcp` (Streamable HTTP, no ASP.NET Core). Check it from a shell, then
connect an agent. The helper scripts used in this guide are in this repository's `scripts/` folder, not in the
packages:

```bash
scripts/mcp-smoke.sh http://127.0.0.1:8787/mcp complete_task '{"id":"task-1"}'
claude mcp add --transport http tasktracker http://127.0.0.1:8787/mcp
```

Platform notes: on iPhone the endpoint answers only while the app is in the foreground. A Mac Catalyst app needs
`com.apple.security.network.server` in its entitlements. From an Android phone, forward the port first
(`adb forward tcp:8787 tcp:8787`).

## Step 5. Front door: Siri and Shortcuts (iOS and Mac Catalyst)

There is no Swift to write. During the build, the package compiles your C#, reads the tools, writes a Swift App Intent
for each one, builds that Swift and puts the intents index (`Metadata.appintents`) into the app bundle. When Siri or
Shortcuts runs an action, the generated Swift calls your C# method by its tool name.

> Needs `Unisono.AppIntents` 0.1.0-preview.3, which is not on nuget.org yet. Until it is, reference
> `src/Unisono.AppIntents` from a clone of this repository and import its `build/Unisono.AppIntents.targets`, as the
> samples do.

**5.1 Attach the bridge at launch.** In `Platforms/iOS/AppDelegate.cs` (and the Mac Catalyst one):

```csharp
public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
{
    var result = base.FinishedLaunching(application, launchOptions);
    var registry = IPlatformApplication.Current!.Services.GetRequiredService<ToolRegistry>();
    Unisono.AppIntents.UnisonoAppIntents.Attach(registry);
    return result;
}
```

**5.2 Help Siri find the app by name (optional).** If the display name is one made-up word, add the spoken names to
`Info.plist`:

```xml
<key>CFBundleSpokenName</key>
<string>Task Tracker</string>
<key>INAlternativeAppNames</key>
<array>
  <dict><key>INAlternativeAppName</key><string>Task Tracker</string></dict>
</array>
```

**5.3 Try it.** Build for the simulator or a device. The build log shows what was generated:

```text
Unisono: generated Swift for 2 intent(s), 1 entity type(s), 1 enum(s); 0 intent(s) hand-written.
```

Install the app and open it once. The Shortcuts app lists the actions and App Shortcuts at once; run one there first.
Siri by voice needs a real device, and it does not need the SiriKit entitlement. Right after installing, Siri may only
find the app while it is in the foreground, until its vocabulary index catches up.

### What the build generates

| From your C# | Siri and Shortcuts get |
|---|---|
| each `[McpServerTool]` | an action named by its `Title`, with its `[Description]`; parameters typed from the signature, defaults kept |
| a required parameter | Siri asks for it ("Title?") instead of failing |
| `Destructive = true` | Siri asks for confirmation before running it |
| each enum a tool uses | a list to pick from |
| a returned entity, list, text, number or yes/no | a result that later Shortcuts steps can use; any other record only reports "done" |
| each `IEntityQuery<T>` (step 3) | an entity Siri can search and pick; a string parameter named `taskId`, or `id` when the app has one entity type, becomes "Which task?" |
| each tool | the phrase "*Title* in *app name*", for example "Create Task in Task Tracker" (iOS allows ten per app) |

### Customising (optional)

The generated Swift is plain on purpose: short replies such as "Create Task: done." and one phrase per action. To
change one piece, put Swift in an `AppIntents/` folder next to the `.csproj`. A type with the same name as a generated
one replaces it, and anything else is added:

- An intent is named after the tool's title: "Create Task" becomes `CreateTaskIntent`. Write a struct with that name
  for your own reply text, a different prompt, or an intent that opens the app.
- An `AppShortcutsProvider` of your own replaces all the generated phrases, because iOS allows one per app.
- Entities are `TaskEntity` and `TaskEntityQuery` for the entity "Task".

A hand-written intent calls the same bridge the generated one does:

```swift
let created = try await UnisonoTools.invoke("create_task", Args(title: taskTitle), returning: TaskDto.self)
return .result(value: TaskEntity(created), dialog: "Created '\(created.title)'")
```

The Notes sample has no Swift at all. The TaskTracker sample keeps hand-written intents for its due-date prompt,
confirmations and phrases, and gets its Get Task action generated.

### Settings

The defaults rarely need changing. These MSBuild properties adjust them:

| Property | Default |
|---|---|
| `UnisonoGenerateSwift` | `true`; set `false` to write all the Swift yourself in `AppIntents/` |
| `UnisonoAppIntentsDir` | `AppIntents` next to the `.csproj` |
| `UnisonoIntentsModuleName` | the assembly name followed by `Intents` |
| `UnisonoIntentsBundleId` | the `ApplicationId` followed by `.intents` |
| `UnisonoIntentsDeploymentTarget` | the app's `SupportedOSPlatformVersion` |
| `UnisonoGenerateXcodeProject` | `true`; set `false` to turn off the whole Apple build step |

An app that declares its own `<XcodeProject>` item is left alone.

## Step 6. Front door: Android AppFunctions (API 36+)

AppFunctions is the surface Gemini and other assistants call on Android. There is no Kotlin or Gradle step.

**6.1 Attach at startup.** In `Platforms/Android/MainApplication.cs`:

```csharp
public override void OnCreate()
{
    base.OnCreate();
    var registry = IPlatformApplication.Current!.Services.GetRequiredService<ToolRegistry>();
    Unisono.AppFunctions.UnisonoAppFunctions.Attach(registry);
}
```

**6.2 Add the service.** An empty subclass; the base class does the dispatching. The name fixes the Java class name
the manifest refers to:

```csharp
[Service(Name = "com.example.tasktracker.TaskTrackerAppFunctionService")]
public class TaskTrackerAppFunctionService : Unisono.AppFunctions.UnisonoAppFunctionService { }
```

**6.3 Declare it in `Platforms/Android/AndroidManifest.xml`,** inside `<application>`:

```xml
<service android:name="com.example.tasktracker.TaskTrackerAppFunctionService"
         android:exported="true"
         android:permission="android.permission.BIND_APP_FUNCTION_SERVICE">
  <intent-filter>
    <action android:name="android.app.appfunctions.AppFunctionService" />
  </intent-filter>
  <property android:name="android.app.appfunctions" android:value="app_functions.xml" />
</service>
```

**6.4 List the functions.** Android indexes the function ids from an XML asset, one entry per tool. Generate it from
the running app's MCP endpoint (step 4), so it always matches the registry:

```bash
adb forward tcp:8787 tcp:8787
python3 scripts/new-appfunctions-xml.py http://127.0.0.1:8787/mcp Platforms/Android/Assets/app_functions.xml
```

Ship it as an asset:

```xml
<ItemGroup Condition="'$(_Platform)' == 'android'">
  <AndroidAsset Include="Platforms\Android\Assets\app_functions.xml" Link="Assets\app_functions.xml" />
</ItemGroup>
```

**6.5 Try it** on a phone with Android 16 (API 36) or newer. It was verified on a Pixel 7 with Android 17; the API 36
emulator image has no `cmd app_function`. Quote the whole command so the JSON keeps its quotes:

```bash
adb shell cmd app_function list-app-functions --package com.example.tasktracker
adb shell "cmd app_function execute-app-function --package com.example.tasktracker --function create_task --parameters '{\"title\":\"Buy oat milk\"}'"
```

## Adding an action later

1. Add the C# method with `[McpServerTool]` and `[Description]`. MCP clients see it on the next launch, with nothing
   else to do.
2. **Android:** re-run `new-appfunctions-xml.py` and rebuild.
3. **Apple:** nothing. The next build generates the intent and its phrase. If the app has its own
   `AppShortcutsProvider`, add the phrase there.

## Where to look next

- `samples/TaskTracker`: everything above in one MAUI app, with eight tools, an entity query and all three front doors.
- `samples/UnisonoNotes`: a second app with no Swift at all, used for the cross-app Shortcut and the agent demos.
- `samples/ConsoleHost`: the smallest host, two tools over MCP with no phone.
- The package READMEs under `src/` and the design notes in `docs/dotnet-siri-mcp-framework-design.md`.
