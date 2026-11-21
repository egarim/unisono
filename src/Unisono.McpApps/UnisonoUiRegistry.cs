using Unisono;

namespace Unisono.McpApps;

/// <summary>
/// Framework-neutral UI registry that binds existing Unisono tools to UI providers.
/// No React, Blazor, or Uno assemblies are referenced by this package.
/// </summary>
public sealed class UnisonoUiRegistry
{
    private readonly ToolRegistry _tools;
    private readonly Dictionary<string, IUnisonoUiProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, UnisonoUiDefinition> _definitions = new(StringComparer.Ordinal);

    public UnisonoUiRegistry(ToolRegistry tools)
        => _tools = tools ?? throw new ArgumentNullException(nameof(tools));

    public IReadOnlyCollection<UnisonoUiDefinition> Definitions => _definitions.Values.ToArray();

    /// <summary>Returns the registered provider for a UI framework.</summary>
    public IUnisonoUiProvider GetProvider(string framework)
        => _providers.TryGetValue(framework, out var provider)
            ? provider
            : throw new KeyNotFoundException($"No provider registered for '{framework}'.");

    public UnisonoUiRegistry AddProvider(IUnisonoUiProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.Framework);
        if (!_providers.TryAdd(provider.Framework, provider))
            throw new InvalidOperationException($"A UI provider for '{provider.Framework}' is already registered.");
        return this;
    }

    public UnisonoUiRegistry MapTool(
        string toolName, string resourceUri, string framework, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(framework);
        if (!_tools.Contains(toolName))
            throw new ArgumentException($"Tool '{toolName}' is not registered in Unisono.Core.", nameof(toolName));
        if (!Uri.TryCreate(resourceUri, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "ui", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("MCP App resources must use an absolute ui:// URI.", nameof(resourceUri));
        if (_definitions.Values.Any(x => string.Equals(x.ResourceUri, resourceUri, StringComparison.Ordinal)))
            throw new InvalidOperationException($"UI resource '{resourceUri}' is already mapped.");
        if (!_providers.ContainsKey(framework))
            throw new InvalidOperationException($"Register the '{framework}' provider before mapping tools.");

        _definitions[toolName] = new UnisonoUiDefinition(toolName, resourceUri, framework, title);
        return this;
    }

    public bool TryGetDefinition(string toolName, out UnisonoUiDefinition? definition)
        => _definitions.TryGetValue(toolName, out definition);

    public async ValueTask<UnisonoUiAsset> GetAssetAsync(
        string toolName,
        CancellationToken cancellationToken = default)
    {
        if (!_definitions.TryGetValue(toolName, out var definition))
            throw new KeyNotFoundException($"Tool '{toolName}' has no UI mapping.");

        var asset = await _providers[definition.Framework]
            .GetAssetAsync(definition, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(asset.ResourceUri, definition.ResourceUri, StringComparison.Ordinal))
            throw new InvalidOperationException("UI provider returned a resource URI different from its mapping.");
        if (string.IsNullOrWhiteSpace(asset.MimeType))
            throw new InvalidOperationException("UI provider must supply a MIME type.");
        return asset;
    }

    /// <summary>Use for graceful fallback when an AI host does not support embedded UI.</summary>
    public bool CanRender(string toolName, UnisonoUiHostCapabilities host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.SupportsMcpApps &&
            _definitions.TryGetValue(toolName, out var definition) &&
            (host.SupportedFrameworks is null || host.SupportedFrameworks.Contains(definition.Framework));
    }
}
