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
    public async Task Without_a_total_a_short_page_is_the_end()
    {
        using var client = Client(_ => Json(200, new JsonObject { ["items"] = Rows(300, 0) }));
        Assert.Equal(300, (await client.ListAsync("tasks")).Count);
    }

    [Fact]
    public async Task A_server_that_ignores_the_offset_is_reported_not_repeated_forever()
    {
        using var client = Client(_ => Json(200, new JsonObject { ["items"] = Rows(500, 0), ["total"] = 1200 }));
        var e = await Assert.ThrowsAsync<McpException>(() => client.ListAsync("tasks"));
        Assert.Contains("ignoring the offset", e.Message);
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

    [Fact]
    public async Task Tasks_emit_only_matching_rows_and_never_nulls()
    {
        var api = new FakeApi(req => req.Method == HttpMethod.Post
            ? TokenOk()
            : Json(200, new JsonObject
            {
                ["items"] = new JsonArray(
                    new JsonObject { ["key"] = "a", ["payload"] = new JsonObject { ["project"] = "p", ["stage"] = "claimed", ["code"] = "A" } },
                    new JsonObject { ["key"] = "b", ["payload"] = new JsonObject { ["project"] = "q", ["stage"] = "claimed", ["code"] = "B" } }),
            }));
        using var client = new VivariumClient(State, new HttpClient(api));
        var tools = new VivariumTools(client);
        var rows = JsonNode.Parse(await tools.Tasks(project: "p"))!.AsArray();
        Assert.Single(rows);
        Assert.Equal("a", rows[0]!["key"]!.GetValue<string>());
    }

    [Fact]
    public void A_state_with_non_string_values_is_named_as_missing()
    {
        var state = JsonNode.Parse("{\"hub\":1,\"auth\":2,\"client\":3,\"refresh\":4}")!.AsObject();
        Assert.Equal(new[] { "hub", "auth", "client", "refresh" }, VivariumClient.Missing(state));
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
