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

    private static JsonObject Device(string name, bool online, string? clone) => Doc(name, new JsonObject
    {
        ["name"] = name, ["status"] = "paired", ["repoPath"] = clone,
        ["lastSeen"] = online ? Now.AddMinutes(-1).ToString("o") : Now.AddHours(-3).ToString("o"),
    });

    private static JsonArray Empty() => new();

    private static JsonArray SetupConversation() => new(Doc("c", new JsonObject { ["kind"] = "setup" }));

    [Fact]
    public void The_state_of_nuvos_on_2026_11_22_names_its_six_gates_in_order()
    {
        // The setup conversation leads: its action is how the two gates after it are cleared.
        var gaps = Blocking.Compute(Project(), Baselines("draft"), Empty(), Empty(), Tasks(), Devices(true), Empty(), Now);
        Assert.Equal(6, gaps.Count);
        Assert.Contains("setup conversation", gaps[0].What);
        Assert.Contains("not signed", gaps[1].What);
        Assert.Contains("no guardrails", gaps[2].What);
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
            new JsonArray(Doc("claude-code", new JsonObject())), Empty(), Devices(true), SetupConversation() };
        Assert.DoesNotContain(Blocking.Compute(Project(), ready[0], ready[1], ready[2], ready[3], ready[4], ready[5], Now), g => g.What.Contains("machine"));
        Assert.Contains(Blocking.Compute(Project(), ready[0], ready[1], ready[2], ready[3], Devices(false), ready[5], Now), g => g.What.Contains("none online"));
    }

    [Fact]
    public void A_clone_held_by_an_offline_machine_does_not_clear_the_clone_gate()
    {
        // The clone must be on a machine that can actually run it (review, 2026-11-23, finding D1): the online
        // machine has no clone, so the gate stands even though a paired machine does.
        var devices = new JsonArray(Device("box-with-clone", online: false, clone: "/home/j/singular"),
                                    Device("box-online", online: true, clone: null));
        var gaps = Blocking.Compute(Project(), Baselines("signed"),
            new JsonArray(Doc("g-1", new JsonObject { ["baseline"] = "singular-1.0" })),
            new JsonArray(Doc("claude-code", new JsonObject())), Empty(), devices, SetupConversation(), Now);
        Assert.Contains(gaps, g => g.What.Contains("clone"));
    }

    [Fact]
    public void A_planned_unstaffed_task_is_the_factory_queue_not_a_gate()
    {
        var planned = new JsonArray(Doc("singular-t-fw-03", new JsonObject
        {
            ["project"] = "singular", ["code"] = "T-FW-03", ["stage"] = "planned",
        }));
        var gaps = Blocking.Compute(Project(), Baselines("signed"),
            new JsonArray(Doc("g-1", new JsonObject { ["baseline"] = "singular-1.0" })),
            new JsonArray(Doc("claude-code", new JsonObject())),
            planned, Devices(true), SetupConversation(), Now);
        Assert.Empty(gaps);
    }

    [Fact]
    public void Just_under_three_minutes_is_online_the_boundary_is_offline_and_just_over_is_too()
    {
        Assert.True(Blocking.WithinHeartbeats(Now.AddSeconds(-179).ToString("o"), Now));
        Assert.False(Blocking.WithinHeartbeats(Now.AddSeconds(-180).ToString("o"), Now));  // strict: three missed beats
        Assert.False(Blocking.WithinHeartbeats(Now.AddSeconds(-181).ToString("o"), Now));
        Assert.False(Blocking.WithinHeartbeats(null, Now));
    }

    [Fact]
    public void An_unreadable_timestamp_is_offline_not_a_crash()
    {
        Assert.False(Blocking.WithinHeartbeats("yesterday", Now));
        Assert.False(Blocking.WithinHeartbeats("", Now));
        // a non-round-trip spelling with an explicit UTC marker still parses to the same instant under an invariant culture
        Assert.True(Blocking.WithinHeartbeats(Now.AddSeconds(-60).ToString("yyyy-MM-dd HH:mm:ss") + "Z", Now));
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

    [Fact]
    public void A_legacy_state_with_a_non_string_tenant_falls_through_to_the_named_path()
    {
        // GetValue<string> on a number throws InvalidOperationException; the locate must not crash on a bad file.
        var home = Directory.CreateTempSubdirectory("viv-companion").FullName;
        Directory.CreateDirectory(Path.Combine(home, ".creatura", "teams"));
        File.WriteAllText(Path.Combine(home, ".creatura", "device.json"), "{\"tenant\":123}");
        Assert.Equal(Path.Combine(home, ".creatura", "teams", "vivarium-nuvos.json"),
                     VivariumClient.LocateState(home, "vivarium-nuvos"));
        Directory.Delete(home, true);
    }

    [Fact]
    public void A_missing_required_key_is_named()
    {
        var state = JsonNode.Parse("{\"hub\":\"https://api\",\"client\":\"c\"}")!.AsObject();
        Assert.Equal(2, VivariumClient.Missing(state).Count);  // auth and refresh
        Assert.Contains("auth", VivariumClient.Missing(state));
        Assert.Contains("refresh", VivariumClient.Missing(state));
    }
}
