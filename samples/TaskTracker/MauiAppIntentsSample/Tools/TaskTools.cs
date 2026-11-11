using System.ComponentModel;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;
using ModelContextProtocol.Server;

namespace MauiAppIntentsSample.Tools;

/// <summary>
/// The one place TaskTracker's actions are defined. Siri (through the App Intents bridge) and
/// MCP clients (through Unisono's in-app server) both end up calling these methods.
/// Descriptions matter: both Siri's model and MCP clients choose tools and fill parameters from them.
/// </summary>
[McpServerToolType]
public sealed class TaskTools(ITaskService tasks)
{
    [McpServerTool(Name = "create_task", Title = "Create Task", Destructive = false, Idempotent = false)]
    [Description("Creates a new task in TaskTracker and returns it.")]
    public TaskDto CreateTask(
        [Description("Short title of the task")] string title,
        [Description("Priority: low, medium, high or urgent")] TaskPriorityLevel priority = TaskPriorityLevel.Medium,
        [Description("Category: work, personal, shopping, health or fitness")] TaskCategoryType category = TaskCategoryType.Personal,
        [Description("Due date and time, ISO 8601")] DateTime? dueDate = null,
        [Description("Estimated effort in minutes, 1-480")] int? estimatedMinutes = null,
        [Description("Free-form notes")] string? notes = null)
        => TaskDto.From(tasks.Create(title, priority, category, dueDate, estimatedMinutes, notes));

    [McpServerTool(Name = "get_task", Title = "Get Task", ReadOnly = true, Destructive = false)]
    [Description("Returns one task by id, or null if it does not exist.")]
    public TaskDto? GetTask([Description("The task id")] string id)
        => tasks.GetById(id) is { } task ? TaskDto.From(task) : null;

    [McpServerTool(Name = "list_tasks", Title = "List Tasks", ReadOnly = true, Destructive = false)]
    [Description("Lists tasks, newest first, optionally filtered by category, priority and due window. Completed tasks are hidden unless showCompleted is true.")]
    public IReadOnlyList<TaskDto> ListTasks(
        [Description("Only tasks in this category")] TaskCategoryType? category = null,
        [Description("Only tasks with this priority")] TaskPriorityLevel? priority = null,
        [Description("Include completed tasks")] bool showCompleted = false,
        [Description("Only tasks due in this window: any, today, tomorrow, thisWeek (next seven days) or overdue")] DueFilter due = DueFilter.Any)
        => tasks.GetFiltered(category, priority, showCompleted).Where(t => Matches(t, due)).Select(TaskDto.From).ToList();

    /// <summary>Due-window test in the device's local calendar; tasks without a due date only match "any".</summary>
    private static bool Matches(TaskItem task, DueFilter due)
    {
        if (due == DueFilter.Any) return true;
        if (task.DueDate is not { } dueDate) return false;
        var day = dueDate.Kind == DateTimeKind.Utc ? dueDate.ToLocalTime().Date : dueDate.Date;
        var today = DateTime.Today;
        return due switch
        {
            DueFilter.Today => day == today,
            DueFilter.Tomorrow => day == today.AddDays(1),
            DueFilter.ThisWeek => day >= today && day < today.AddDays(7),
            DueFilter.Overdue => day < today && !task.IsCompleted,
            _ => true,
        };
    }

    [McpServerTool(Name = "search_tasks", Title = "Search Tasks", ReadOnly = true, Destructive = false)]
    [Description("Finds tasks whose title or notes contain the query text.")]
    public IReadOnlyList<TaskDto> SearchTasks([Description("Text to look for")] string query)
        => tasks.Search(query).Select(TaskDto.From).ToList();

    [McpServerTool(Name = "complete_task", Title = "Complete Task", Idempotent = true, Destructive = false)]
    [Description("Marks a task as completed. Returns false if the task does not exist.")]
    public bool CompleteTask([Description("The task id")] string id) => tasks.Complete(id);

    [McpServerTool(Name = "delete_task", Title = "Delete Task", Idempotent = true, Destructive = true)]
    [Description("Deletes a task permanently. Returns false if the task does not exist.")]
    public bool DeleteTask([Description("The task id")] string id) => tasks.Delete(id);

    [McpServerTool(Name = "delete_completed_tasks", Title = "Delete Completed Tasks", Idempotent = true, Destructive = true)]
    [Description("Deletes every completed task and returns how many were removed.")]
    public int DeleteCompletedTasks()
    {
        var completed = tasks.GetFiltered(showCompleted: true).Where(t => t.IsCompleted).Select(t => t.Id).ToList();
        return completed.Count(tasks.Delete);
    }

    [McpServerTool(Name = "set_due_date", Title = "Set Due Date", Idempotent = true, Destructive = false)]
    [Description("Sets or changes the due date of a task. Returns false if the task does not exist.")]
    public bool SetDueDate(
        [Description("The task id")] string id,
        [Description("New due date and time, ISO 8601")] DateTime dueDate)
        => tasks.SetDueDate(id, dueDate);
}

/// <summary>Wire representation of a task, shared by the MCP result and the Siri bridge.</summary>
public sealed record TaskDto(
    string Id,
    string Title,
    string? Notes,
    TaskPriorityLevel Priority,
    TaskCategoryType Category,
    DateTime? DueDate,
    int? EstimatedMinutes,
    bool IsCompleted,
    DateTime CreatedAt)
{
    public static TaskDto From(TaskItem t) =>
        new(t.Id, t.Title, t.Notes, t.Priority, t.Category, t.DueDate, t.EstimatedMinutes, t.IsCompleted, t.CreatedAt);
}
