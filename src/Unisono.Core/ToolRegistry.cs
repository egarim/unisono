using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Unisono;

/// <summary>
/// One tool definition, several front doors. The registry holds every tool the app exposes.
/// Siri (through the generated App Intents bridge), MCP clients (through the in-app MCP server)
/// and in-app agents all invoke tools through it, so each action has exactly one handler.
/// </summary>
/// <remarks>
/// M0: tools are discovered with reflection from methods marked with the MCP SDK's own
/// <see cref="McpServerToolAttribute"/> and <see cref="DescriptionAttribute"/>. Later milestones
/// replace reflection with a generated descriptor table (trim/AOT friendly).
/// </remarks>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, Entry> _tools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EntityAdapter> _entities = new(StringComparer.Ordinal);

    public ToolRegistry(JsonSerializerOptions? serializerOptions = null)
    {
        SerializerOptions = serializerOptions ?? CreateDefaultSerializerOptions();
    }

    /// <summary>Options used to bind tool arguments and serialise tool results. Shared by every front door.</summary>
    public JsonSerializerOptions SerializerOptions { get; }

    /// <summary>Tool names in registration order.</summary>
    public IReadOnlyCollection<string> Names => _tools.Keys.ToArray();

    /// <summary>The tools as MCP server primitives, for <c>McpServerOptions.ToolCollection</c>.</summary>
    public IReadOnlyCollection<McpServerTool> McpTools => _tools.Values.Select(e => e.McpTool).ToArray();

    /// <summary>The tools as <see cref="AIFunction"/>s, for in-app agents (Microsoft.Extensions.AI chat clients).</summary>
    public IReadOnlyCollection<AIFunction> Functions => _tools.Values.Select(e => e.Function).ToArray();

    public bool Contains(string name) => _tools.ContainsKey(name);

    /// <summary>Entity type names registered with <see cref="AddEntityQuery{T}"/>.</summary>
    public IReadOnlyCollection<string> EntityTypes => _entities.Keys.ToArray();

    public static JsonSerializerOptions CreateDefaultSerializerOptions()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        // The SDK's defaults already include a plain string-enum converter; ours must come first to win.
        options.Converters.Insert(0, new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    /// <summary>
    /// Registers every public method on <paramref name="instance"/> that carries
    /// <see cref="McpServerToolAttribute"/>. The method's <see cref="DescriptionAttribute"/> and its
    /// parameters' descriptions become the tool description and the JSON schema descriptions.
    /// </summary>
    public ToolRegistry AddToolsFrom(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var type = instance.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var method in type.GetMethods(flags))
        {
            var attr = method.GetCustomAttribute<McpServerToolAttribute>();
            if (attr is null)
                continue;

            var name = string.IsNullOrWhiteSpace(attr.Name) ? method.Name : attr.Name;
            var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;

            var function = AIFunctionFactory.Create(method, method.IsStatic ? null : instance, new AIFunctionFactoryOptions
            {
                Name = name,
                Description = description,
                SerializerOptions = SerializerOptions,
            });

            var mcpTool = McpServerTool.Create(function, new McpServerToolCreateOptions
            {
                Name = name,
                Description = description,
                Title = attr.Title,
                Destructive = attr.Destructive,
                Idempotent = attr.Idempotent,
                OpenWorld = attr.OpenWorld,
                ReadOnly = attr.ReadOnly,
                UseStructuredContent = attr.UseStructuredContent,
                SerializerOptions = SerializerOptions,
            });

            _tools[name] = new Entry(function, mcpTool);
        }

        return this;
    }

    /// <summary>
    /// Invokes a tool by name with JSON arguments (property names as in the tool's schema) and
    /// returns the JSON result, or <c>null</c> for a void result. This is the path the native
    /// bridges use: Swift (App Intents) and later Kotlin/Android (AppFunctions) hand over JSON and
    /// get JSON back, so the native layer never needs to know the C# types.
    /// </summary>
    public async ValueTask<JsonNode?> InvokeAsync(string name, JsonObject? arguments = null, CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(name, out var entry))
            throw new KeyNotFoundException($"Unisono: no tool named '{name}'. Known tools: {string.Join(", ", _tools.Keys)}");

        var args = new AIFunctionArguments();
        if (arguments is not null)
        {
            foreach (var pair in arguments)
                args[pair.Key] = pair.Value is null ? null : JsonSerializer.SerializeToElement(pair.Value, SerializerOptions);
        }

        var result = await entry.Function.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            null => null,
            JsonElement element => element.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(element.GetRawText()),
            JsonNode node => node,
            _ => JsonSerializer.SerializeToNode(result, SerializerOptions),
        };
    }

    /// <summary>Typed convenience over <see cref="InvokeAsync(string, JsonObject?, CancellationToken)"/>.</summary>
    public async ValueTask<T?> InvokeAsync<T>(string name, JsonObject? arguments = null, CancellationToken cancellationToken = default)
    {
        var node = await InvokeAsync(name, arguments, cancellationToken).ConfigureAwait(false);
        return node is null ? default : node.Deserialize<T>(SerializerOptions);
    }

    /// <summary>
    /// Registers how an entity type is looked up. <paramref name="typeName"/> is the name the native side uses
    /// (for example "Task"); the results are serialised with <see cref="SerializerOptions"/>.
    /// </summary>
    public ToolRegistry AddEntityQuery<T>(string typeName, IEntityQuery<T> query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(query);

        _entities[typeName] = new EntityAdapter(
            async (ids, ct) => ToArray(await query.ByIdsAsync(ids, ct).ConfigureAwait(false)),
            async (text, ct) => ToArray(await query.SearchAsync(text, ct).ConfigureAwait(false)),
            async ct => ToArray(await query.SuggestedAsync(ct).ConfigureAwait(false)));
        return this;

        JsonArray ToArray(IReadOnlyList<T> items)
            => JsonSerializer.SerializeToNode(items, SerializerOptions) as JsonArray ?? new JsonArray();
    }

    public ValueTask<JsonArray> EntitiesByIdsAsync(string typeName, IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
        => Entity(typeName).ByIds(ids, cancellationToken);

    public ValueTask<JsonArray> SearchEntitiesAsync(string typeName, string text, CancellationToken cancellationToken = default)
        => Entity(typeName).Search(text, cancellationToken);

    public ValueTask<JsonArray> SuggestedEntitiesAsync(string typeName, CancellationToken cancellationToken = default)
        => Entity(typeName).Suggested(cancellationToken);

    private EntityAdapter Entity(string typeName)
        => _entities.TryGetValue(typeName, out var adapter)
            ? adapter
            : throw new KeyNotFoundException($"Unisono: no entity query for '{typeName}'. Known types: {string.Join(", ", _entities.Keys)}");

    private sealed record Entry(AIFunction Function, McpServerTool McpTool);

    private sealed record EntityAdapter(
        Func<IReadOnlyList<string>, CancellationToken, ValueTask<JsonArray>> ByIds,
        Func<string, CancellationToken, ValueTask<JsonArray>> Search,
        Func<CancellationToken, ValueTask<JsonArray>> Suggested);
}
