using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using Unisono.Samples.VivariumCompanion;

namespace VivariumCompanion.Tests;

// The client is the half that talks to a real deployment, so its paging and its error surfaces are tested against a
// fake API: a server that clamps the page size, one that reports a total, one that ignores the offset, and the
// token endpoint's own error body (review, 2026-11-23).
public class ClientTests
{
    private static readonly JsonObject State = JsonNode.Parse(
        "{\"hub\":\"https://api.example\",\"auth\":\"https://auth.example\",\"client\":\"c\",\"refresh\":\"r\",\"tenant\":\"t\"}")!.AsObject();

    private sealed class FakeApi(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }

    private static HttpResponseMessage Json(int code, JsonNode node) => new((HttpStatusCode)code)
    {
        Content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage TokenOk() =>
        Json(200, new JsonObject { ["access_token"] = "access", ["expires_in"] = 300 });

    private static VivariumClient Client(Func<HttpRequestMessage, HttpResponseMessage?> data)
    {
        var api = new FakeApi(req =>
        {
            if (req.Method == HttpMethod.Post) return TokenOk();
            return data(req) ?? Json(404, new JsonObject { ["error"] = "unexpected call" });
        });
        return new VivariumClient(State, new HttpClient(api));
    }

    private static (int Limit, int Offset) Page(HttpRequestMessage req)
    {
        var q = req.RequestUri!.Query.TrimStart('?').Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => int.Parse(p[1]));
        return (q["limit"], q["offset"]);
    }

    private static JsonArray Rows(int count, int from) =>
        new(Enumerable.Range(from, count).Select(i => (JsonNode)new JsonObject
        {
            ["key"] = $"k{i}",
            ["payload"] = new JsonObject(),
        }).ToArray());

    [Fact]
    public async Task A_server_that_clamps_the_page_size_is_still_read_whole()
    {
        using var client = Client(req =>
        {
            var (_, offset) = Page(req);
            return Json(200, new JsonObject { ["items"] = Rows(Math.Min(100, 1200 - offset), offset), ["total"] = 1200 });
        });
        Assert.Equal(1200, (await client.ListAsync("tasks")).Count);
    }

    [Fact]
    public async Task The_reported_total_ends_the_walk_even_on_an_exact_multiple()
    {
        using var client = Client(req =>
        {
            var (_, offset) = Page(req);
            return Json(200, new JsonObject { ["items"] = Rows(Math.Min(500, 1000 - offset), offset), ["total"] = 1000 });
        });
        Assert.Equal(1000, (await client.ListAsync("tasks")).Count);
    }

    [Fact]
    public async Task Without_a_total_a_clamped_server_is_still_read_whole()
    {
        // A server that honors the offset but answers fewer rows per page than asked: the walk ends at the empty page.
        using var client = Client(req =>
        {
            var (_, offset) = Page(req);
            return Json(200, new JsonObject { ["items"] = Rows(Math.Max(0, Math.Min(300, 1200 - offset)), offset) });
        });
        Assert.Equal(1200, (await client.ListAsync("tasks")).Count);
    }

    [Fact]
    public async Task An_empty_first_page_is_an_empty_collection()
    {
        using var client = Client(_ => Json(200, new JsonObject { ["items"] = new JsonArray() }));
        Assert.Empty(await client.ListAsync("tasks"));
    }

    [Fact]
    public async Task An_empty_page_before_the_reported_total_is_an_error()
    {
        using var client = Client(req =>
        {
            var (_, offset) = Page(req);
            return Json(200, new JsonObject
            {
                ["items"] = offset == 0 ? Rows(500, 0) : new JsonArray(),
                ["total"] = 1200,
            });
        });
        var e = await Assert.ThrowsAsync<McpException>(() => client.ListAsync("tasks"));
        Assert.Contains("empty page", e.Message);
    }

    [Fact]
    public async Task A_server_that_ignores_the_offset_is_reported_not_repeated_forever()
    {
        using var client = Client(_ => Json(200, new JsonObject { ["items"] = Rows(500, 0), ["total"] = 1200 }));
        var e = await Assert.ThrowsAsync<McpException>(() => client.ListAsync("tasks"));
        Assert.Contains("repeating pages", e.Message);
    }

    [Fact]
    public async Task Past_the_row_cap_the_refusal_names_the_cap_not_the_offset()
    {
        // a server paging correctly forever still stops, and the message says what actually happened (review round 4)
        using var client = Client(req =>
        {
            var (_, offset) = Page(req);
            return Json(200, new JsonObject { ["items"] = Rows(500, offset) });
        });
        var e = await Assert.ThrowsAsync<McpException>(() => client.ListAsync("tasks"));
        Assert.Contains("companion's limit of 200000", e.Message);
        Assert.DoesNotContain("offset", e.Message);
    }

    [Fact]
    public async Task A_total_just_past_the_cap_is_refused_too()
    {
        // the cap holds with a total as well: 200,001 rows must not be handed back (review round 5)
        using var client = Client(req =>
        {
            var (_, offset) = Page(req);
            return Json(200, new JsonObject { ["items"] = Rows(500, offset), ["total"] = 200_001 });
        });
        var e = await Assert.ThrowsAsync<McpException>(() => client.ListAsync("tasks"));
        Assert.Contains("companion's limit of 200000", e.Message);
    }

    [Fact]
    public async Task A_total_at_the_cap_is_read_whole()
    {
        using var client = Client(req =>
        {
            var (_, offset) = Page(req);
            return Json(200, new JsonObject { ["items"] = Rows(500, offset), ["total"] = 200_000 });
        });
        Assert.Equal(200_000, (await client.ListAsync("tasks")).Count);
    }

    [Fact]
    public async Task A_pairing_refused_by_the_token_endpoint_says_to_re_pair()
    {
        var api = new FakeApi(_ => Json(400, new JsonObject { ["error"] = "invalid_grant" }));
        using var client = new VivariumClient(State, new HttpClient(api));
        var e = await Assert.ThrowsAsync<McpException>(() => client.ListAsync("tasks"));
        Assert.Contains("invalid_grant", e.Message);
        Assert.Contains("re-pair", e.Message);
    }

    [Fact]
    public async Task A_gateway_error_does_not_tell_the_person_to_re_pair()
    {
        var api = new FakeApi(_ => Json(503, new JsonObject { ["error"] = "unavailable" }));
        using var client = new VivariumClient(State, new HttpClient(api));
        var e = await Assert.ThrowsAsync<McpException>(() => client.ListAsync("tasks"));
        Assert.Contains("503", e.Message);
        Assert.DoesNotContain("re-pair", e.Message);
    }

    [Fact]
    public async Task A_data_api_refusal_surfaces_the_servers_own_body()
    {
        var api = new FakeApi(req => req.Method == HttpMethod.Post
            ? TokenOk()
            : Json(401, new JsonObject { ["error"] = "the token was revoked" }));
        using var client = new VivariumClient(State, new HttpClient(api));
        var e = await Assert.ThrowsAsync<McpException>(() => client.ListAsync("tasks"));
        Assert.Contains("401", e.Message);
        Assert.Contains("revoked", e.Message);
    }

    /// A server whose one page holds `items` at offset 0 and is empty afterwards (a correct end-of-walk).
    private static HttpResponseMessage? ServeOnce(JsonArray items, HttpRequestMessage req) => Page(req).Offset == 0
        ? Json(200, new JsonObject { ["items"] = items })
        : Json(200, new JsonObject { ["items"] = new JsonArray() });

    [Fact]
    public async Task Tasks_emit_only_matching_rows_and_never_nulls()
    {
        var rows = new JsonArray(
            new JsonObject { ["key"] = "a", ["payload"] = new JsonObject { ["project"] = "p", ["stage"] = "claimed", ["code"] = "A" } },
            new JsonObject { ["key"] = "b", ["payload"] = new JsonObject { ["project"] = "q", ["stage"] = "claimed", ["code"] = "B" } });
        using var client = Client(req => ServeOnce(rows, req));
        var tools = new VivariumTools(client);
        var emitted = JsonNode.Parse(await tools.Tasks(project: "p"))!.AsArray();
        Assert.Single(emitted);
        Assert.Equal("a", emitted[0]!["key"]!.GetValue<string>());
    }

    [Fact]
    public void A_state_with_non_string_values_is_named_as_missing()
    {
        var state = JsonNode.Parse("{\"hub\":1,\"auth\":2,\"client\":3,\"refresh\":4}")!.AsObject();
        Assert.Equal(new[] { "hub", "auth", "client", "refresh" }, VivariumClient.Missing(state));
    }

    [Fact]
    public async Task An_alias_with_a_non_string_first_key_falls_through_to_the_working_one()
    {
        // {"hub": 1, "api": "…"} passes the startup refusal through "api" and must read through the same choice
        var state = JsonNode.Parse("{\"hub\":1,\"api\":\"https://api.example\",\"auth\":\"https://auth.example\"," +
                                   "\"client\":\"c\",\"refresh\":\"r\"}")!.AsObject();
        Assert.Empty(VivariumClient.Missing(state));
        using (var owned = new VivariumClient(state)) Assert.Equal("https://api.example", owned.Api);
        var api = new FakeApi(req => req.Method == HttpMethod.Post
            ? TokenOk()
            : Json(200, new JsonObject { ["items"] = new JsonArray() }));
        using var client = new VivariumClient(state, new HttpClient(api));
        Assert.Empty(await client.ListAsync("tasks"));
    }

    [Fact]
    public async Task An_injected_http_client_is_left_to_its_caller_on_dispose()
    {
        var api = new FakeApi(req => req.Method == HttpMethod.Post ? TokenOk() : ServeOnce(Rows(1, 0), req)!);
        using var http = new HttpClient(api);
        var client = new VivariumClient(State, http);
        Assert.Single(await client.ListAsync("tasks"));
        client.Dispose();
        // the injected client must still work: an unconditional dispose would throw ObjectDisposedException here
        var second = new VivariumClient(State, http);
        Assert.Single(await second.ListAsync("tasks"));
    }

    [Fact]
    public async Task Guardrails_are_emitted_as_values_not_encoded_strings()
    {
        var rows = new JsonArray(new JsonObject
        {
            ["key"] = "g-1",
            ["payload"] = new JsonObject
            {
                ["baseline"] = "singular-1.0",
                ["checks"] = new JsonArray("dotnet build", "dotnet test"),
                ["deniedCommands"] = new JsonArray("git push"),
                ["tokenBudget"] = 8000000,
            },
        });
        using var client = Client(req => ServeOnce(rows, req));
        var emitted = JsonNode.Parse(await new VivariumTools(client).Guardrails())!.AsArray();
        Assert.Single(emitted);
        Assert.IsType<JsonArray>(emitted[0]!["checks"]);
        Assert.Equal(8000000, emitted[0]!["tokenBudget"]!.GetValue<int>());
    }

    [Fact]
    public void A_legacy_state_path_that_is_a_directory_does_not_crash_the_locate()
    {
        var home = Directory.CreateTempSubdirectory("viv-companion-").FullName;
        Directory.CreateDirectory(Path.Combine(home, ".creatura", "device.json"));
        Directory.CreateDirectory(Path.Combine(home, ".creatura", "teams"));
        Assert.Equal(Path.Combine(home, ".creatura", "teams", "vivarium-nuvos.json"),
                     VivariumClient.LocateState(home, "vivarium-nuvos"));
        Directory.Delete(home, true);
    }
}
