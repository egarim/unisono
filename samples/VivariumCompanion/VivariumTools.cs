using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace Unisono.Samples.VivariumCompanion;

/// The companion's tools: reads over the team's collections, plus the diagnose that names the gates in order.
/// Everything is ReadOnly; the person's approvals, signatures and passkeys stay in the app, where they belong.
[McpServerToolType]
public sealed class VivariumTools
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private readonly VivariumClient _v;

    public VivariumTools(VivariumClient v) => _v = v;

    private static string Rows(JsonArray items, Func<JsonObject, JsonObject> shape) =>
        JsonSerializer.Serialize(items.OfType<JsonObject>().Select(shape).ToArray(), Json);

    [McpServerTool(Name = "vivarium_projects", ReadOnly = true, Destructive = false)]
    [Description("Every project of the team: key, name, current baseline, status.")]
    public async Task<string> Projects(CancellationToken ct = default) =>
        Rows(await _v.ListAsync("projects", ct: ct), d => new JsonObject
        {
            ["key"] = d["key"]?.GetValue<string>(),
            ["name"] = d["payload"]?["name"]?.GetValue<string>(),
            ["baseline"] = d["payload"]?["currentBaseline"]?.GetValue<string>(),
            ["status"] = d["payload"]?["status"]?.GetValue<string>(),
        });

    [McpServerTool(Name = "vivarium_baselines", ReadOnly = true, Destructive = false)]
    [Description("The baselines of one project (or all): version, status, signedBy, signedAt. A draft baseline blocks every run.")]
    public async Task<string> Baselines([Description("Project key, as vivarium_projects shows it")] string? project = null, CancellationToken ct = default)
    {
        var items = await _v.ListAsync("baselines", ct: ct);
        var rows = items.OfType<JsonObject>()
            .Where(d => project is null || d["payload"]?["project"]?.GetValue<string>() == project)
            .Select(d => new JsonObject
            {
                ["key"] = d["key"]?.GetValue<string>(),
                ["project"] = d["payload"]?["project"]?.GetValue<string>(),
                ["version"] = d["payload"]?["version"]?.GetValue<string>(),
                ["status"] = d["payload"]?["status"]?.GetValue<string>(),
                ["signedBy"] = d["payload"]?["signedBy"]?.GetValue<string>(),
            }).ToArray();
        return JsonSerializer.Serialize(rows, Json);
    }

    [McpServerTool(Name = "vivarium_tasks", ReadOnly = true, Destructive = false)]
    [Description("Tasks of one project (or all), optionally one stage: code, title, stage, assignee, agent, device, branch.")]
    public async Task<string> Tasks([Description("Project key")] string? project = null,
        [Description("Stage: planned, claimed, running, checks, review, merged, cancelled")] string? stage = null, CancellationToken ct = default)
    {
        var items = await _v.ListAsync("tasks", ct: ct);
        return Rows(items, d =>
        {
            var p = d["payload"]?.AsObject();
            if (project is not null && p?["project"]?.GetValue<string>() != project) return null!;
            if (stage is not null && p?["stage"]?.GetValue<string>() != stage) return null!;
            return new JsonObject
            {
                ["key"] = d["key"]?.GetValue<string>(),
                ["code"] = p?["code"]?.GetValue<string>(),
                ["title"] = p?["title"]?.GetValue<string>(),
                ["stage"] = p?["stage"]?.GetValue<string>(),
                ["assignee"] = p?["assignee"]?.GetValue<string>(),
                ["agent"] = p?["agent"]?.GetValue<string>(),
                ["device"] = p?["device"]?.GetValue<string>(),
                ["branch"] = p?["branch"]?.GetValue<string>(),
                ["conversation"] = p?["conversation"]?.GetValue<string>(),
            };
        });
    }

    [McpServerTool(Name = "vivarium_runs", ReadOnly = true, Destructive = false)]
    [Description("The team's runs, newest first: number, task, status, started, ended, device, tokens vs budget.")]
    public async Task<string> Runs([Description("Task key to limit to")] string? task = null, CancellationToken ct = default)
    {
        var items = await _v.ListAsync("runs", ct: ct);
        var rows = items.OfType<JsonObject>()
            .Where(d => task is null || d["payload"]?["task"]?.GetValue<string>() == task)
            .OrderByDescending(d => d["payload"]?["startedAt"]?.GetValue<string>() ?? "")
            .Take(50)
            .Select(d => new JsonObject
            {
                ["key"] = d["key"]?.GetValue<string>(),
                ["task"] = d["payload"]?["task"]?.GetValue<string>(),
                ["status"] = d["payload"]?["status"]?.GetValue<string>(),
                ["started"] = d["payload"]?["startedAt"]?.GetValue<string>(),
                ["ended"] = d["payload"]?["endedAt"]?.GetValue<string>(),
                ["device"] = d["payload"]?["device"]?.GetValue<string>(),
                ["tokens"] = d["payload"]?["tokensTotal"]?.GetValue<int>(),
            }).ToArray();
        return JsonSerializer.Serialize(rows, Json);
    }

    [McpServerTool(Name = "vivarium_devices", ReadOnly = true, Destructive = false)]
    [Description("The team's machines: name, os, status, online (three missed one-minute heartbeats make it offline), agents, clone path, last seen.")]
    public async Task<string> Devices(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var items = await _v.ListAsync("devices", ct: ct);
        var rows = items.OfType<JsonObject>().Select(d => new JsonObject
        {
            ["key"] = d["key"]?.GetValue<string>(),
            ["name"] = d["payload"]?["name"]?.GetValue<string>(),
            ["os"] = d["payload"]?["os"]?.GetValue<string>(),
            ["status"] = d["payload"]?["status"]?.GetValue<string>(),
            ["online"] = Blocking.WithinHeartbeats(d["payload"]?["lastSeen"]?.GetValue<string>(), now),
            ["agents"] = d["payload"]?["agents"]?.GetValue<string>(),
            ["clone"] = d["payload"]?["repoPath"]?.GetValue<string>(),
            ["lastSeen"] = d["payload"]?["lastSeen"]?.GetValue<string>(),
        }).ToArray();
        return JsonSerializer.Serialize(rows, Json);
    }

    [McpServerTool(Name = "vivarium_conversations", ReadOnly = true, Destructive = false)]
    [Description("The team's conversations: key, kind (setup, task, free), task, title.")]
    public async Task<string> Conversations(CancellationToken ct = default) =>
        Rows(await _v.ListAsync("conversations", ct: ct), d => new JsonObject
        {
            ["key"] = d["key"]?.GetValue<string>(),
            ["kind"] = d["payload"]?["kind"]?.GetValue<string>(),
            ["task"] = d["payload"]?["task"]?.GetValue<string>(),
            ["title"] = d["payload"]?["title"]?.GetValue<string>(),
        });

    [McpServerTool(Name = "vivarium_what_is_blocking", ReadOnly = true, Destructive = false)]
    [Description("The gates a project must pass before work can run, in order, each with the one action that clears it: signed baseline, guardrails, a setup conversation, agent kinds, staffed tasks, linked conversations, a paired and online machine with a clone. Start here.")]
    public async Task<string> WhatIsBlocking([Description("Project key, as vivarium_projects shows it")] string project, CancellationToken ct = default)
    {
        try
        {
            var projects = await _v.ListAsync("projects", ct: ct);
            var p = projects.OfType<JsonObject>().FirstOrDefault(x => x["key"]?.GetValue<string>() == project
                || string.Equals(x["payload"]?["name"]?.GetValue<string>(), project, StringComparison.OrdinalIgnoreCase));
            if (p is null) return JsonSerializer.Serialize(new { error = $"no project named {project}" }, Json);
            var baselines = await _v.ListAsync("baselines", ct: ct);
            var guardrails = await _v.ListAsync("guardrails", ct: ct);
            var agentKinds = await _v.ListAsync("agent_kinds", ct: ct);
            var tasks = await _v.ListAsync("tasks", ct: ct);
            var devices = await _v.ListAsync("devices", ct: ct);
            var conversations = await _v.ListAsync("conversations", ct: ct);
            var gaps = Blocking.Compute(p, baselines, guardrails, agentKinds, tasks, devices, conversations, DateTime.UtcNow);
            return JsonSerializer.Serialize(gaps.Select(g => new { gap = g.What, action = g.Action }).ToArray(), Json);
        }
        catch (Exception e)
        {
            // A diagnose that hides why it failed is worse than useless: the message is the next clue.
            return JsonSerializer.Serialize(new { error = e.Message }, Json);
        }
    }
}
