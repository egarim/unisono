using System.Text.Json;
using System.Text.Json.Nodes;
using Foundation;
using MauiAppIntentsSample.Binding;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Tools;
using Unisono;

namespace MauiAppIntentsSample.Platforms.iOS;

/// <summary>
/// Implements the Swift TaskDataProvider protocol in C#.
/// Every call is turned into a JSON tool invocation on the Unisono registry, so a Siri request
/// runs exactly the same handler as an MCP tools/call. (M1 replaces this hand-written mapping
/// with one generic invoke(name, json) bridge and generated Swift intents.)
/// </summary>
public class AppIntentsBridgeProvider(ToolRegistry registry) : TaskDataProvider
{
    public override BridgeTaskItem[] GetAllTasks()
        => (Call<List<TaskDto>>("list_tasks", new JsonObject { ["showCompleted"] = true }) ?? []).Select(ToBridge).ToArray();

    public override BridgeTaskItem? GetTask(string id)
        => Call<TaskDto>("get_task", new JsonObject { ["id"] = id }) is { } task ? ToBridge(task) : null;

    public override BridgeTaskItem? CreateTask(string title, nint priorityRawValue, nint categoryRawValue,
        NSDate? dueDate, nint estimatedMinutes, string notes)
    {
        var args = new JsonObject
        {
            ["title"] = title,
            ["priority"] = Arg((TaskPriorityLevel)(int)priorityRawValue),
            ["category"] = Arg((TaskCategoryType)(int)categoryRawValue),
        };
        if (dueDate is not null) args["dueDate"] = Arg((DateTime)dueDate);
        if ((int)estimatedMinutes >= 0) args["estimatedMinutes"] = (int)estimatedMinutes;
        if (!string.IsNullOrEmpty(notes)) args["notes"] = notes;

        return Call<TaskDto>("create_task", args) is { } task ? ToBridge(task) : null;
    }

    public override bool CompleteTask(string id)
        => Call<bool>("complete_task", new JsonObject { ["id"] = id });

    public override BridgeTaskItem[] SearchTasks(string query)
        => (Call<List<TaskDto>>("search_tasks", new JsonObject { ["query"] = query }) ?? []).Select(ToBridge).ToArray();

    public override BridgeTaskItem[] GetTasksByFilter(nint categoryRawValue, nint priorityRawValue, bool showCompleted)
    {
        var args = new JsonObject { ["showCompleted"] = showCompleted };
        if ((int)categoryRawValue >= 0) args["category"] = Arg((TaskCategoryType)(int)categoryRawValue);
        if ((int)priorityRawValue >= 0) args["priority"] = Arg((TaskPriorityLevel)(int)priorityRawValue);
        return (Call<List<TaskDto>>("list_tasks", args) ?? []).Select(ToBridge).ToArray();
    }

    public override bool SetDueDate(NSDate date, string taskId)
        => Call<bool>("set_due_date", new JsonObject { ["id"] = taskId, ["dueDate"] = Arg((DateTime)date) });

    // App Intents run perform() off the main thread, so blocking here is safe and keeps the
    // synchronous @objc protocol simple. The generic M1 bridge will use completion blocks instead.
    private T? Call<T>(string tool, JsonObject args)
    {
        try
        {
            return registry.InvokeAsync<T>(tool, args).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Unisono] tool '{tool}' failed: {ex}");
            return default;
        }
    }

    private JsonNode? Arg<T>(T value) => JsonSerializer.SerializeToNode(value, registry.SerializerOptions);

    private static BridgeTaskItem ToBridge(TaskDto t) => new(
        id: t.Id,
        title: t.Title,
        notes: t.Notes ?? "",
        priorityRawValue: (nint)(int)t.Priority,
        categoryRawValue: (nint)(int)t.Category,
        dueDate: t.DueDate.HasValue ? (NSDate)t.DueDate.Value : null,
        estimatedMinutes: t.EstimatedMinutes ?? -1,
        isCompleted: t.IsCompleted,
        createdAt: (NSDate)t.CreatedAt);
}
