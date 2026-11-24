using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ModelContextProtocol;

namespace Unisono.Samples.VivariumCompanion;

/// Talks to a Singular deployment the way the paired device does: the same state file, the same refresh grant, the
/// same paging. It reads with GET; the only two writes it has are CreateAnswerAsync and SetTaskDeviceAsync, each behind
/// its own grant, checked here at the write. It never writes the state file back, so a device daemon refreshing beside
/// it is never overwritten (the rotated refresh stays in this process only).
public sealed class VivariumClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Grants? _grants;
    private readonly JsonObject _state;
    private readonly SemaphoreSlim _refresh = new(1, 1);  // two tools asking at once refresh the rotating token once
    private string? _token;
    private DateTime _exp = DateTime.MinValue;

    /// `http` lets a test drive the client against a fake deployment; in production the client owns its one.
    /// Without `grants` nothing can be written.
    public VivariumClient(JsonObject state, HttpClient? http = null, Grants? grants = null)
    {
        _grants = grants;
        _state = state;
        _http = http ?? new HttpClient();
        _ownsHttp = http is null;
    }

    public string Api => (StringOf(_state, "hub", "api")
        ?? throw new McpException("the pairing state has no hub address - re-pair this machine")).TrimEnd('/');
    public string Tenant => StringOf(_state, "tenant") ?? "vivarium-dev";
    private string App => StringOf(_state, "app") ?? "vivarium";
    private string Auth => (StringOf(_state, "auth", "keycloak")
        ?? throw new McpException("the pairing state has no sign-in address - re-pair this machine")).TrimEnd('/');
    private string Realm => StringOf(_state, "realm") ?? "xafplatform";
    private string ClientId => StringOf(_state, "client")
        ?? throw new McpException("the pairing state has no client id - re-pair this machine");

    /// The keys a usable pairing must carry, as strings. Program refuses to start the server without them, with the
    /// reason instead of a NullReferenceException or a type error on the first call (review, 2026-11-23).
    public static IReadOnlyList<string> Missing(JsonObject state)
    {
        var missing = new List<string>();
        if (StringOf(state, "hub", "api") is null) missing.Add("hub");
        if (StringOf(state, "auth", "keycloak") is null) missing.Add("auth");
        if (StringOf(state, "client") is null) missing.Add("client");
        if (StringOf(state, "refresh") is null) missing.Add("refresh");
        return missing;
    }

    /// The first key of `keys` whose value is a non-blank JSON string. The check and the accessors share it, so a
    /// state that passes the startup refusal reads the same way here (review round 3: {"hub": 1, "api": "…"}).
    private static string? StringOf(JsonObject state, params string[] keys) =>
        keys.Select(k => state[k] as JsonValue)
            .Select(v => v is not null && v.TryGetValue<string>(out var text) ? text : null)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

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
                                      or NotSupportedException or UnauthorizedAccessException)
        {
            // no usable legacy pairing (missing, corrupt, a directory, unreadable): fall through to the named path,
            // whose absence the caller reports - never a stack trace before the server starts
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
            {
                var status = (int)r.StatusCode;
                // Only a refusal of the pairing itself means "re-pair"; a gateway error does not.
                var hint = status is 400 or 401 or 403 ? " - this machine's pairing is expired or revoked; re-pair it" : "";
                throw new McpException($"the pairing's token refresh was refused ({status}): {Snippet(body)}{hint}");
            }
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
    /// total or at an empty page (a server that clamps the page size is still read whole, because the offset
    /// advances by the rows actually returned), and a server that repeats pages is reported instead of looped
    /// (review, 2026-11-23).
    public async Task<JsonArray> ListAsync(string collection, string? filter = null, CancellationToken ct = default)
    {
        const int Page = 500, MaxRows = 200_000;
        var items = new JsonArray();
        var offset = 0;
        int? total = null;
        while (true)
        {
            var q = $"limit={Page}&offset={offset}" + (filter is null ? "" : $"&{filter}");
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
            total ??= root?["total"] is JsonValue t && t.TryGetValue<int>(out var n) ? n : null;
            foreach (var it in page) items.Add(it?.DeepClone());
            if (items.Count > MaxRows)  // the cap holds whether or not a total was reported (review round 5)
                throw new McpException($"{collection}: read {items.Count} rows, past the companion's limit of {MaxRows}; " +
                                       "refusing to read further");
            if (total is not null && items.Count > total)
                throw new McpException($"{collection}: read {items.Count} rows against a reported total of {total}; " +
                                       "the API is repeating pages");
            if (total is not null && items.Count >= total) return items;   // the total is the contract
            if (page.Count == 0)
            {
                if (total is not null)  // fewer rows than the server itself promised
                    throw new McpException($"{collection}: the API reported {total} rows but answered an empty page " +
                                           $"at offset {offset}");
                return items;  // nothing more to read
            }
            offset += page.Count;  // advance by what the server actually returned, never by the limit asked for
        }
    }

    /// A document key as the hub's jobs and the app's documents use: never a path.
    public static bool IsKey(string? key) =>
        key is not null && Regex.IsMatch(key, @"\A[A-Za-z0-9._:@+-]{1,200}\z") && key.Any(c => c != '.');

    /// A filter: field = this JSON string.
    public static string Eq(string field, string value) => $"{field}=eq.{Uri.EscapeDataString(JsonSerializer.Serialize(value))}";

    private async Task<(int Status, string Body)> SendAsync(HttpMethod method, string path, JsonObject? payload, string? ifMatch, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, $"{Api}/api/app/{path}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
        req.Headers.TryAddWithoutValidation("X-App-Id", App);
        req.Headers.TryAddWithoutValidation("X-Tenant-Id", Tenant);
        if (payload is not null)
        {
            req.Content = new StringContent(payload.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
            // As singular-js writes it (the revision is not quoted); never a write without a condition.
            if (ifMatch is null) req.Headers.TryAddWithoutValidation("If-None-Match", "*");
            else req.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }
        using var r = await _http.SendAsync(req, ct);
        return ((int)r.StatusCode, await r.Content.ReadAsStringAsync(ct));
    }

    /// One document, or null when it is absent.
    public async Task<JsonObject?> GetAsync(string collection, string key, CancellationToken ct = default)
    {
        if (!IsKey(key)) throw new McpException("that is not a document key");
        var (status, body) = await SendAsync(HttpMethod.Get, $"data/{collection}/{Uri.EscapeDataString(key)}", null, null, ct);
        if (status == 404) return null;
        if (status is < 200 or > 299) throw new McpException($"the data API answered {status} reading {collection}: {Snippet(body)}");
        return JsonNode.Parse(body) as JsonObject;
    }

    public sealed record Me(string Subject, IReadOnlyList<string> Roles, bool Administrator)
    {
        /// The hub's rule for answers: a lead, or the team's administrator.
        public bool Leads => Administrator || Roles.Contains("lead");
    }

    /// Who the pairing signs in as, as the server says it now.
    public async Task<Me> WhoAmIAsync(CancellationToken ct = default)
    {
        var (status, body) = await SendAsync(HttpMethod.Get, "whoami", null, null, ct);
        if (status is < 200 or > 299) throw new McpException($"the API answered {status} to who this is: {Snippet(body)}");
        var o = JsonNode.Parse(body) as JsonObject;
        var subject = o is null ? null : StringOf(o, "subject");
        if (subject is null) throw new McpException("the API did not say who this computer's person is");
        var roles = (o!["roles"] as JsonArray ?? []).OfType<JsonValue>().Select(r => r.TryGetValue<string>(out var s) ? s : "").ToList();
        return new Me(subject, roles, o["isTenantAdministrator"] is JsonValue a && a.TryGetValue<bool>(out var yes) && yes);
    }

    /// Writes a person's answer, only if no message has that key. False when one has.
    public async Task<bool> CreateAnswerAsync(string key, string conversation, string author, string text, CancellationToken ct = default)
    {
        if (!Factory.IsAnswerKey(key)) throw new McpException("that is not an answer's key");
        if (Factory.HasDecision(text)) (_grants ?? throw new McpException("this companion writes nothing")).RequireDecision();
        return await WriteAsync(Grants.AnswerJob, "messages", key, new JsonObject
        {
            ["conversation"] = conversation, ["author"] = author, ["role"] = "person",
            ["createdAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture), ["text"] = text,
        }, null, ct);
    }

    /// Changes a task's device and nothing else, only while the task is at the revision that was read. False when not.
    public async Task<bool> SetTaskDeviceAsync(JsonObject task, string device, CancellationToken ct = default)
    {
        if (task["revision"] is not JsonValue v || !v.TryGetValue<long>(out var revision) || revision < 1 || task["payload"] is not JsonObject payload)
            throw new McpException("refused: the API gave no revision for this task, so it cannot be changed safely");
        var changed = payload.DeepClone().AsObject();
        changed["device"] = device;
        return await WriteAsync(Grants.MoveTask, "tasks", Factory.Key(task), changed,
            revision.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
    }

    private async Task<bool> WriteAsync(string grant, string collection, string key, JsonObject payload, string? ifMatch, CancellationToken ct)
    {
        if (!IsKey(key)) throw new McpException("that is not a document key");
        (_grants ?? throw new McpException("this companion writes nothing")).Require(grant);
        int status;
        string body;
        try
        {
            (status, body) = await SendAsync(HttpMethod.Put, $"data/{collection}/{Uri.EscapeDataString(key)}", payload, ifMatch, ct);
        }
        catch (Exception e) when (e is HttpRequestException or IOException || e is TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new McpException("the write's outcome is not known (no answer from the API): call again with the same arguments, which never writes it twice");
        }
        if (status is 409 or 412) return false;
        if (status is < 200 or > 299) throw new McpException($"the data API answered {status} writing {collection}: {Snippet(body)}");
        return true;
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
        _refresh.Dispose();
    }
}
