using System.Text.Json.Nodes;

namespace Unisono.Samples.VivariumCompanion;

/// Talks to a Singular deployment the way the paired device does: the same state file, the same refresh grant, the
/// same paging. Read-only by construction - only GET is offered - and it never writes the state file back, so a
/// device daemon refreshing beside it is never stepped on (the rotated refresh stays in this process only).
public sealed class VivariumClient : IDisposable
{
    private readonly HttpClient _http = new();
    private readonly JsonObject _state;
    private string? _token;
    private DateTime _exp = DateTime.MinValue;

    public VivariumClient(JsonObject state) => _state = state;

    public string Api => (_state["hub"] ?? _state["api"])!.GetValue<string>().TrimEnd('/');
    public string Tenant => _state["tenant"]?.GetValue<string>() ?? "vivarium-dev";
    private string App => _state["app"]?.GetValue<string>() ?? "vivarium";
    private string Auth => (_state["auth"] ?? _state["keycloak"])!.GetValue<string>().TrimEnd('/');
    private string Realm => _state["realm"]?.GetValue<string>() ?? "xafplatform";
    private string ClientId => _state["client"]!.GetValue<string>();

    /// Where a machine keeps its pairing for a team, mirroring creatura_device.team_state_path: the first team
    /// paired keeps device.json, every further team gets teams/&lt;team&gt;.json.
    public static string LocateState(string home, string? team)
    {
        var legacy = Path.Combine(home, ".creatura", "device.json");
        var teams = Path.Combine(home, ".creatura", "teams");
        if (string.IsNullOrEmpty(team)) return legacy;
        var named = Path.Combine(teams, team + ".json");
        if (File.Exists(named)) return named;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(legacy)) is JsonObject s && (s["tenant"]?.GetValue<string>() == team))
                return legacy;
        }
        catch (IOException) { /* no legacy pairing: fall through to the named path, whose absence the caller reports */ }
        catch (System.Text.Json.JsonException) { }
        return named;
    }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        if (_token is not null && _exp > DateTime.UtcNow.AddSeconds(60)) return _token;
        using var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("refresh_token", _state["refresh"]!.GetValue<string>()),
        });
        using var r = await _http.PostAsync($"{Auth}/realms/{Realm}/protocol/openid-connect/token", form, ct);
        r.EnsureSuccessStatusCode();
        var j = JsonNode.Parse(await r.Content.ReadAsStringAsync(ct))!.AsObject();
        _token = j["access_token"]!.GetValue<string>();
        _exp = DateTime.UtcNow.AddSeconds(j["expires_in"]?.GetValue<int>() ?? 300);
        if (j["refresh_token"] is JsonNode rotated) _state["refresh"] = rotated.DeepClone();
        return _token;
    }

    /// Every page of a collection, newest-safe: the caller orders what it reads.
    public async Task<JsonArray> ListAsync(string collection, string? filter = null, CancellationToken ct = default)
    {
        var items = new JsonArray();
        for (var offset = 0; ; offset += 500)
        {
            var q = $"limit=500&offset={offset}" + (filter is null ? "" : $"&{filter}");
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{Api}/api/app/data/{collection}?{q}");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
            req.Headers.TryAddWithoutValidation("X-App-Id", App);
            req.Headers.TryAddWithoutValidation("X-Tenant-Id", Tenant);
            using var r = await _http.SendAsync(req, ct);
            r.EnsureSuccessStatusCode();
            var page = JsonNode.Parse(await r.Content.ReadAsStringAsync(ct))!["items"] as JsonArray ?? new JsonArray();
            foreach (var it in page) items.Add(it?.DeepClone());
            if (page.Count < 500) return items;
        }
    }

    public void Dispose() => _http.Dispose();
}
