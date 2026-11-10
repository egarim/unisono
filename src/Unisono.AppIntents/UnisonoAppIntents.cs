using System.Text.Json.Nodes;

namespace Unisono.AppIntents;

/// <summary>
/// Connects the app's Swift App Intents (compiled with native/UnisonoBridge.swift) to a <see cref="ToolRegistry"/>.
/// Call <see cref="Attach"/> once at launch, before Siri or Shortcuts can run an intent.
/// </summary>
public static class UnisonoAppIntents
{
    private static RegistryHost? s_host;

    public static void Attach(ToolRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        s_host = new RegistryHost(registry); // the Swift side only holds a weak reference
        UnisonoBridge.Shared.Host = s_host;
    }

    public static bool IsAttached => UnisonoBridge.Shared.Host is not null;
}

/// <summary>The C# end of the bridge: JSON in, JSON out, one registry.</summary>
internal sealed class RegistryHost(ToolRegistry registry) : UnisonoHost
{
    public override void Invoke(string tool, string argumentsJson, UnisonoCompletion completion)
        => Run(completion, async () =>
        {
            JsonObject? args = null;
            if (!string.IsNullOrWhiteSpace(argumentsJson))
                args = JsonNode.Parse(argumentsJson) as JsonObject
                       ?? throw new ArgumentException("Tool arguments must be a JSON object.");

            var result = await registry.InvokeAsync(tool, args).ConfigureAwait(false);
            return result?.ToJsonString(registry.SerializerOptions);
        });

    public override void Entities(string type, string[] ids, UnisonoCompletion completion)
        => Run(completion, async () =>
            (await registry.EntitiesByIdsAsync(type, ids).ConfigureAwait(false)).ToJsonString(registry.SerializerOptions));

    public override void SearchEntities(string type, string text, UnisonoCompletion completion)
        => Run(completion, async () =>
            (await registry.SearchEntitiesAsync(type, text).ConfigureAwait(false)).ToJsonString(registry.SerializerOptions));

    public override void SuggestedEntities(string type, UnisonoCompletion completion)
        => Run(completion, async () =>
            (await registry.SuggestedEntitiesAsync(type).ConfigureAwait(false)).ToJsonString(registry.SerializerOptions));

    private static void Run(UnisonoCompletion completion, Func<Task<string?>> work)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                completion(await work().ConfigureAwait(false), null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Unisono] bridge call failed: {ex}");
                completion(null, ex.Message);
            }
        });
    }
}
