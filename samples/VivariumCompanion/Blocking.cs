using System.Globalization;
using System.Text.Json.Nodes;

namespace Unisono.Samples.VivariumCompanion;

/// The gates a project must pass before work can run, in the order a person meets them, each with the one action
/// that clears it. Pure over the payloads the API returns, so it is testable without a deployment. This is the tool
/// the owner asked for by name of the pain: "after [the first steps] nothing is clear".
public static class Blocking
{
    public record Gap(string What, string Action);

    private static readonly string[] OpenStages = ["planned", "claimed", "running", "checks"];

    private static JsonObject? P(JsonNode? doc) => doc?["payload"] as JsonObject;

    public static IReadOnlyList<Gap> Compute(JsonObject project, JsonArray baselines, JsonArray guardrails,
        JsonArray agentKinds, JsonArray tasks, JsonArray devices, JsonArray conversations, DateTime now)
    {
        var gaps = new List<Gap>();
        var key = project["key"]?.GetValue<string>() ?? "";
        var version = P(project)?["currentBaseline"]?.GetValue<string>();

        // The setup conversation comes first: it is how the baseline and the guardrails are proposed, so the gaps
        // below are impossible to clear without it (review, 2026-11-23).
        if (!conversations.Any(c => P(c)?["kind"]?.GetValue<string>() == "setup"))
            gaps.Add(new Gap("the team has no setup conversation with Curator: the baseline and guardrails are proposed there",
                "Conversations place, Setup group, Start setup with Curator"));

        var baseline = baselines.FirstOrDefault(b => P(b)?["project"]?.GetValue<string>() == key
            && (version is null || P(b)?["version"]?.GetValue<string>() == version));
        var bp = P(baseline);
        if (baseline is null)
            gaps.Add(new Gap($"project {key} has no baseline record for {version ?? "any version"}",
                "plan baseline 1.0 with Curator in a setup conversation"));
        else if (bp?["status"]?.GetValue<string>() != "signed")
            gaps.Add(new Gap($"baseline {version} of {key} is {bp?["status"]?.GetValue<string>() ?? "unknown"}, not signed",
                "sign it: ask Curator in the setup conversation to propose it (write_baseline) and approve the card"));

        var baselineKey = baseline?["key"]?.GetValue<string>();
        if (baselineKey is not null && !guardrails.Any(g => P(g)?["baseline"]?.GetValue<string>() == baselineKey))
            gaps.Add(new Gap($"no guardrails for baseline {version} of {key}",
                "ask Curator in the setup conversation to propose them (set_guardrails) and approve the card"));

        if (agentKinds.Count == 0)
            gaps.Add(new Gap("no agent kinds exist, so no task can name an agent",
                "MORE, Allowed agents, New: add claude-code (a lead edit in the app)"));

        var mine = tasks.Where(t => P(t)?["project"]?.GetValue<string>() == key).ToList();
        // A planned task with nobody on it is the factory's queue, not a defect: the factory carries planned tasks
        // itself. A task a person holds (claimed and beyond) with no assignee or agent can never run.
        var held = mine.Where(t => P(t)?["stage"]?.GetValue<string>() is { } s && s != "planned" && OpenStages.Contains(s)).ToList();
        var unstaffed = held.Where(t => string.IsNullOrEmpty(P(t)?["assignee"]?.GetValue<string>())
            || string.IsNullOrEmpty(P(t)?["agent"]?.GetValue<string>())).ToList();
        if (unstaffed.Count > 0)
            gaps.Add(new Gap($"{unstaffed.Count} open task(s) without assignee or agent: {string.Join(", ", unstaffed.Take(5).Select(t => P(t)?["code"]?.GetValue<string>() ?? t!["key"]?.GetValue<string>() ?? "?"))}",
                "open each task and set assignee and agent"));

        var unlinked = mine.Where(t => P(t)?["stage"]?.GetValue<string>() is { } s2 && (s2 == "claimed" || s2 == "running")
            && string.IsNullOrEmpty(P(t)?["conversation"]?.GetValue<string>())
            && !conversations.Any(c => P(c)?["task"]?.GetValue<string>() == t!["key"]?.GetValue<string>())).ToList();
        if (unlinked.Count > 0)
            gaps.Add(new Gap($"{unlinked.Count} claimed/running task(s) with no conversation: {string.Join(", ", unlinked.Take(5).Select(t => P(t)?["code"]?.GetValue<string>() ?? t!["key"]?.GetValue<string>() ?? "?"))}",
                "open the task in the app so its conversation opens as you"));

        var paired = devices.Where(d => P(d)?["status"]?.GetValue<string>() != "revoked").ToList();
        if (paired.Count == 0)
            gaps.Add(new Gap("no paired machine: nothing can execute a run",
                "pair one with setup.sh (macOS/Linux) or setup.ps1 (Windows)"));
        else
        {
            var online = paired.Where(d => WithinHeartbeats(P(d)?["lastSeen"]?.GetValue<string>(), now)).ToList();
            if (online.Count == 0)
                gaps.Add(new Gap($"{paired.Count} paired machine(s) but none online",
                    "start the listener: creatura_device.py run on the machine"));
            else if (!online.Any(d => !string.IsNullOrEmpty(P(d)?["repoPath"]?.GetValue<string>())))
                gaps.Add(new Gap("no online machine has a clone attached",
                    $"creatura_device.py clone {key} <folder> on the machine that will run it"));
        }
        return gaps;
    }

    /// deviceOnline on the wire, exactly as the app defines it (deviceOnline.ts): three missed one-minute
    /// heartbeats make a machine offline - a 3-minute window, strict. A second definition here would let the
    /// companion and the app disagree about the same machine (review, 2026-11-23).
    public static bool WithinHeartbeats(string? lastSeen, DateTime now) =>
        lastSeen is not null
        && DateTimeOffset.TryParse(lastSeen, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var seen)
        && now - seen.UtcDateTime < TimeSpan.FromMinutes(3);
}
