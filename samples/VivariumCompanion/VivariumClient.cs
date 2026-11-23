using System.Text.Json.Nodes;
using ModelContextProtocol;

namespace Unisono.Samples.VivariumCompanion;

/// Talks to a Singular deployment the way the paired device does: the same state file, the same refresh grant, the
/// same paging. Read-only by construction - only GET is offered - and it never writes the state file back, so a
/// device daemon refreshing beside it is never stepped on (the rotated refresh stays in this process only).
public sealed class VivariumClient : IDisposable
{
    private readonly HttpClient _http = new();
    private readonly JsonObject _state;
    private readonly SemaphoreSlim _refresh = new(1, 1);  // two tools asking at once refresh the rotating token once
    private string? _token;
    private DateTime _exp = DateTime.MinValue;

    public VivariumClient(JsonObject state) => _state = state;

    public string Api => (_state["hub"] ?? _state["api"])!.GetValue<string>().TrimEnd('/');
    public string Tenant => _state["tenant"]?.GetValue<string>() ?? "vivarium-dev";
    private string App => _state["app"]?.GetValue<string>() ?? "vivarium";
    private string Auth => (_state["auth"] ?? _state["keycloak"])!.GetValue<string>().TrimEnd('/');
    private string Realm => _state["realm"]?.GetValue<string>() ?? "xafplatform";
    private string ClientId => _state["client"]!.GetValue<string>();

    /// The keys a usable pairing must carry. Program refuses to start the server without them, with the reason
    /// instead of a NullReferenceException on the first call (review, 2026-11-23).
    public static IReadOnlyList<string> Missing(JsonObject state)
    {
        var missing = new List<string>();
        if (state["hub"] is null && state["api"] is null) missing.Add("hub");
        if (state["auth"] is null && state["keycloak"] is null) missing.Add("auth");
        if (state["client"] is null) missing.Add("client");
        if (state["refresh"] is null) missing.Add("refresh");
        return missing;
    }

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
            if (JsonNode.Parse(File.ReadAllText(legacy)) is JsonObject s && s["tenant"]?.GetValue<string>() == team)
                return legacy;
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException
                                      or NotSupportedException)
        {
            // no usable legacy pairing: fall through to the named path, whose absence the caller reports
        }
        return named;
    }

    private static string Snippet(string body)
    {
        var text = string.Join(" ", body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= 300 ? text : text[..300] + "\u2026";
    }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        if (_token is not null && _exp > DateTime.UtcNow.AddSeconds(60)) return _token;
        await _refresh.WaitAsync(ct);
        try
        {
            if (_token is not null && _exp > DateTime.UtcNow.AddSeconds(60)) return _token;
            using var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("refresh_token", _state["refresh"]!.GetValue<string>()),
            });
            using var r = await _http.PostAsync($"{Auth}/realms/{Realm}/protocol/openid-connect/token", form, ct);
            var body = await r.Content.ReadAsStringAsync(ct);
            if (!r.IsSuccessStatusCode)
                throw new McpException($"the pairing's token refresh was refused ({(int)r.StatusCode}): {Snippet(body)} - " +
                                       "this machine's pairing is expired or revoked; re-pair it");
            var j = JsonNode.Parse(body)!.AsObject();
            _token = j["access_token"]?.GetValue<string>()
                ?? throw new McpException($"the token endpoint answered without an access token: {Snippet(body)}");
            _exp = DateTime.UtcNow.AddSeconds(j["expires_in"]?.GetValue<int>() ?? 300);
            if (j["refresh_token"] is JsonNode rotated) _state["refresh"] = rotated.DeepClone();
            return _token;
        }
        finally
        {
            _refresh.Release();
        }
    }

    /// Every page of a collection, newest-safe: the caller orders what it reads. The walk ends at the server's
    /// total, at a short page, or at the page cap (a server ignoring the offset must not spin forever).
    public async Task<JsonArray> ListAsync(string collection, string? filter = null, CancellationToken ct = default)
    {
        const int Page = 500, MaxPages = 200;
        var items = new JsonArray();
        for (var pageIndex = 0; pageIndex < MaxPages; pageIndex++)
        {
            var q = $"limit={Page}&offset={pageIndex * Page}" + (filter is null ? "" : $"&{filter}");
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{Api}/api/app/data/{collection}?{q}");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
            req.Headers.TryAddWithoutValidation("X-App-Id", App);
            req.Headers.TryAddWithoutValidation("X-Tenant-Id", Tenant);
            using var r = await _http.SendAsync(req, ct);
            var body = await r.Content.ReadAsStringAsync(ct);
            if (!r.IsSuccessStatusCode)
                throw new McpException($"the data API answered {(int)r.StatusCode} reading {collection}: {Snippet(body)}");
            var root = JsonNode.Parse(body);
            var page = root?["items"] as JsonArray ?? new JsonArray();
            foreach (var it in page) items.Add(it?.DeepClone());
            var total = root?["total"] is JsonValue t && t.TryGetValue<int>(out var n) ? n : (int?)null;
            if (page.Count < Page || (total is not null && items.Count >= total)) return items;
        }
        throw new McpException($"{collection}: gave up after {MaxPages} pages of {Page}; the API is ignoring the offset");
    }

    public void Dispose()
    {
        _http.Dispose();
        _refresh.Dispose();
    }
}
