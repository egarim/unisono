namespace Unisono.McpApps;

/// <summary>Associates an MCP tool with a UI resource without depending on a UI toolkit.</summary>
public sealed record UnisonoUiDefinition(
    string ToolName,
    string ResourceUri,
    string Framework,
    string? Title = null);

/// <summary>A built web asset supplied by an adapter (Uno, Blazor, HTML, etc.).</summary>
public sealed record UnisonoUiAsset(
    string ResourceUri,
    string MimeType,
    ReadOnlyMemory<byte> Content);

/// <summary>
/// A UI framework integration. Implementations compile or locate UI assets and return
/// resources that can later be exposed through the MCP Apps resources protocol.
/// </summary>
public interface IUnisonoUiProvider
{
    string Framework { get; }

    ValueTask<UnisonoUiAsset> GetAssetAsync(
        UnisonoUiDefinition definition,
        CancellationToken cancellationToken = default);
}

/// <summary>Describes UI capabilities of a target MCP host.</summary>
public sealed record UnisonoUiHostCapabilities(
    bool SupportsMcpApps,
    IReadOnlySet<string>? SupportedFrameworks = null);
