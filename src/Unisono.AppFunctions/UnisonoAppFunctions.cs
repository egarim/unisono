using Unisono;

namespace Unisono.AppFunctions;

/// <summary>
/// Entry point for the Android projection. Call <see cref="Attach"/> once at application start (for example in
/// <c>MainApplication.OnCreate</c>) so the <see cref="UnisonoAppFunctionService"/> the system binds can reach the
/// registry. The same registry serves MCP and, on Apple platforms, App Intents.
/// </summary>
public static class UnisonoAppFunctions
{
    private static ToolRegistry? _registry;

    /// <summary>The registry attached for this process, or null before <see cref="Attach"/> ran.</summary>
    public static ToolRegistry? Registry => _registry;

    /// <summary>Makes <paramref name="registry"/> the one AppFunction calls are dispatched to.</summary>
    public static void Attach(ToolRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>
    /// Maps an AppFunction id to a registry tool name. Ids may be the bare tool name ("create_task") or
    /// "something#create_task" (the Jetpack convention is "&lt;service class&gt;#&lt;function&gt;").
    /// </summary>
    public static string ToolNameFromFunctionId(string functionId)
    {
        var hash = functionId.LastIndexOf('#');
        return hash >= 0 ? functionId[(hash + 1)..] : functionId;
    }
}
