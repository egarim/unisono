using System.Text.Json.Nodes;
using Unisono.Samples.VivariumCompanion;

namespace VivariumCompanion.Tests;

// The diagnose is the reason this companion exists, so its ordering and its gaps are the contract under test:
// the state of team vivarium-nuvos on 2026-11-22 (draft baseline, no guardrails, no setup conversation, no agent
// kinds, two running tasks with no agent) must come back as exactly those gates, in order.
public class BlockingTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 18, 0, 0, DateTimeKind.Utc);

    private static JsonObject Doc(string key, JsonObject payload) => new() { ["key"] = key, ["payload"] = payload };

    private static JsonObject Project() => Doc("singular", new JsonObject
    {
        ["name"] = "Singular Framework", ["currentBaseline"] = "1.0", ["status"] = "active",
    });

    private static JsonArray Baselines(string status) => new(Doc("singular-1.0", new JsonObject
    {
        ["project"] = "singular", ["version"] = "1.0", ["status"] = status,
    }));

    private static JsonArray Tasks() => new(Doc("singular-t-fw-01", new JsonObject
    {
        ["project"] = "singular", ["code"] = "T-FW-01", ["stage"] = "running",
        ["assignee"] = "subject-1", ["agent"] = null, ["conversation"] = null,
    }));

    private static JsonArray Devices(bool online) => new(Doc("dev-1", new JsonObject
    {
        ["name"] = "Joses-MacBook-Air.local", ["status"] = "paired", ["repoPath"] = "/Users/j/viv/singular",
        ["lastSeen"] = online ? Now.AddMinutes(-1).ToString("o") : Now.AddHours(-3).ToString("o"),
    }));

    private static JsonArray Empty() => new();

    [Fact]
    public void The_state_of_nuvos_on_2026_11_22_names_its_six_gates_in_order()
    {
        var gaps = Blocking.Compute(Project(), Baselines("draft"), Empty(), Empty(), Tasks(), Devices(true), Empty(), Now);
        Assert.Equal(6, gaps.Count);
        Assert.Contains("not signed", gaps[0].What);
        Assert.Contains("no guardrails", gaps[1].What);
        Assert.Contains("setup conversation", gaps[2].What);
        Assert.Contains("agent kinds", gaps[3].What);
        Assert.Contains("T-FW-01", gaps[4].What);
        Assert.Contains("no conversation", gaps[5].What);
        Assert.All(gaps, g => Assert.False(string.IsNullOrWhiteSpace(g.Action)));
    }

    [Fact]
    public void A_ready_project_has_nothing_blocking()
    {
        var guardrails = new JsonArray(Doc("g-1", new JsonObject { ["baseline"] = "singular-1.0" }));
        var kinds = new JsonArray(Doc("claude-code", new JsonObject { ["name"] = "Claude Code" }));
        var tasks = new JsonArray(Doc("singular-t-fw-01", new JsonObject
        {
            ["project"] = "singular", ["code"] = "T-FW-01", ["stage"] = "running",
            ["assignee"] = "subject-1", ["agent"] = "claude-code", ["conversation"] = "conv-1",
        }));
        var convs = new JsonArray(Doc("conv-0", new JsonObject { ["kind"] = "setup" }), Doc("conv-1", new JsonObject { ["kind"] = "task", ["task"] = "singular-t-fw-01" }));
        var gaps = Blocking.Compute(Project(), Baselines("signed"), guardrails, kinds, tasks, Devices(true), convs, Now);
        Assert.Empty(gaps);
    }

    [Fact]
    public void An_existing_task_conversation_counts_as_linked()
    {
        var convs = new JsonArray(Doc("conv-singular-t-fw-01", new JsonObject { ["kind"] = "task", ["task"] = "singular-t-fw-01" }));
        var gaps = Blocking.Compute(Project(), Baselines("signed"),
            new JsonArray(Doc("g-1", new JsonObject { ["baseline"] = "singular-1.0" })),
            new JsonArray(Doc("claude-code", new JsonObject())),
            Tasks(), Devices(true), convs, Now);
        Assert.DoesNotContain(gaps, g => g.What.Contains("with no conversation"));
    }

    [Fact]
    public void A_silent_machine_is_a_gate_and_a_listening_one_is_not()
    {
        var ready = new JsonArray[] { Baselines("signed"), new JsonArray(Doc("g-1", new JsonObject { ["baseline"] = "singular-1.0" })),
            new JsonArray(Doc("claude-code", new JsonObject())), Empty(), Devices(true), new JsonArray(Doc("c", new JsonObject { ["kind"] = "setup" })) };
        Assert.DoesNotContain(Blocking.Compute(Project(), ready[0], ready[1], ready[2], ready[3], ready[4], ready[5], Now), g => g.What.Contains("machine"));
        Assert.Contains(Blocking.Compute(Project(), ready[0], ready[1], ready[2], ready[3], Devices(false), ready[5], Now), g => g.What.Contains("none online"));
    }

    [Fact]
    public void Four_minutes_is_online_and_five_is_not()
    {
        Assert.True(Blocking.WithinHeartbeats(Now.AddMinutes(-4).ToString("o"), Now));
        Assert.False(Blocking.WithinHeartbeats(Now.AddMinutes(-5).ToString("o"), Now));
        Assert.False(Blocking.WithinHeartbeats(null, Now));
    }
}

public class StateLocateTests
{
    [Fact]
    public void The_first_team_keeps_device_json_and_the_second_gets_its_own_file()
    {
        var home = Directory.CreateTempSubdirectory("viv-companion").FullName;
        Directory.CreateDirectory(Path.Combine(home, ".creatura", "teams"));
        File.WriteAllText(Path.Combine(home, ".creatura", "device.json"), "{\"tenant\":\"vivarium-nuvos\"}");
        File.WriteAllText(Path.Combine(home, ".creatura", "teams", "other.json"), "{\"tenant\":\"other\"}");
        Assert.Equal(Path.Combine(home, ".creatura", "device.json"), VivariumClient.LocateState(home, "vivarium-nuvos"));
        Assert.Equal(Path.Combine(home, ".creatura", "teams", "other.json"), VivariumClient.LocateState(home, "other"));
        Assert.Equal(Path.Combine(home, ".creatura", "teams", "third.json"), VivariumClient.LocateState(home, "third"));
        Directory.Delete(home, true);
    }
}
