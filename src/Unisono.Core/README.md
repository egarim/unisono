# Unisono.Core

Declare an app action once in C#; Siri, Shortcuts, any MCP client and in-app agents call the same handler.

`Unisono.Core` is the registry at the centre of [Unisono](https://github.com/egarim/unisono). You mark ordinary
methods with the MCP C# SDK's `[McpServerTool]` and `[Description]` attributes; the `ToolRegistry` turns them into
`AIFunction`s with JSON schemas, invokes them from JSON (which is what the native bridges send), and exposes them as
MCP tools. `IEntityQuery<T>` resolves entities (by id, by text, suggested) for Siri and Shortcuts pickers.

```bash
dotnet add package Unisono.Core --prerelease
```

```csharp
[McpServerToolType]
public sealed class TaskTools(ITaskService tasks)
{
    [McpServerTool(Name = "create_task"), Description("Creates a new task and returns it.")]
    public TaskDto CreateTask([Description("Short title")] string title, TaskPriority priority = TaskPriority.Medium)
        => TaskDto.From(tasks.Create(title, priority));
}

var registry = new ToolRegistry()
    .AddToolsFrom(new TaskTools(service))
    .AddEntityQuery("Task", new TaskEntityQuery(service));
```

Pair it with `Unisono.Mcp` (in-app MCP server) and `Unisono.AppIntents` (Siri and Shortcuts on iOS and Mac Catalyst).
Works with .NET MAUI, Uno Platform, Avalonia and plain .NET apps. Documentation and samples:
https://github.com/egarim/unisono
