using MauiAppIntentsSample.Services;
using Unisono;

namespace MauiAppIntentsSample.Tools;

/// <summary>How Siri and Shortcuts resolve a "Task" entity: by id, by text, or from a suggested list.</summary>
public sealed class TaskEntityQuery(ITaskService tasks) : IEntityQuery<TaskDto>
{
    public ValueTask<IReadOnlyList<TaskDto>> ByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
        => new(ids.Select(tasks.GetById).Where(t => t is not null).Select(t => TaskDto.From(t!)).ToList());

    public ValueTask<IReadOnlyList<TaskDto>> SearchAsync(string text, CancellationToken cancellationToken = default)
        => new(tasks.Search(text).Select(TaskDto.From).ToList());

    public ValueTask<IReadOnlyList<TaskDto>> SuggestedAsync(CancellationToken cancellationToken = default)
        => new(tasks.GetAll().OrderBy(t => t.IsCompleted).Select(TaskDto.From).ToList());
}
