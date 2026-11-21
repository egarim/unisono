using System.ComponentModel;
using ModelContextProtocol.Server;
using Unisono;

namespace Unisono.UI.Catalog.Tests;

/// <summary>Tool shapes the catalog generator must handle, including one it must refuse.</summary>
[McpServerToolType]
public sealed class TestTools
{
    [McpServerTool(Name = "calculate_pupusas", ReadOnly = true)]
    [Description("Calculate the total price of pupusas.")]
    public decimal CalculatePupusas(
        [Description("Number of pupusas")] int quantity,
        [Description("Price per pupusa")] decimal priceEach)
        => quantity * priceEach;

    [McpServerTool(Name = "add_task")]
    [Description("Add a task to the tracker.")]
    public string AddTask(
        [Description("The task title")] string title,
        [Description("Optional due date")] DateOnly? due = null,
        [Description("Where it goes")] Priority priority = Priority.Normal,
        [Description("Minutes to finish")] int? estimate = null)
        => "ok";

    [McpServerTool(Name = "list_tasks", ReadOnly = true)]
    [Description("List tasks.")]
    public string[] ListTasks([Description("Filter by text")] string? query = null) => [];

    [McpServerTool(Name = "search_notes")]
    [Description("Search notes.")]
    public string SearchNotes([Description("Tags to match")] string[] tags) => "";
}

public enum Priority
{
    Low,
    Normal,
    High,
}

internal static class TestRegistry
{
    public static ToolRegistry Create() => new ToolRegistry().AddToolsFrom(new TestTools());
}
