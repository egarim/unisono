namespace Unisono;

/// <summary>
/// Resolves an entity type for the assistant surfaces. Siri and Shortcuts call these when a tool parameter is an
/// entity: by id when they already know it, by text when the user said "the dentist task", and the suggested list
/// when they need the user to pick. The same contract backs Android AppFunctions and MCP resource lookups later.
/// </summary>
public interface IEntityQuery<T>
{
    ValueTask<IReadOnlyList<T>> ByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<T>> SearchAsync(string text, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<T>> SuggestedAsync(CancellationToken cancellationToken = default);
}
