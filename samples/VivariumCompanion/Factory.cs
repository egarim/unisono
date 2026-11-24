using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Unisono.Samples.VivariumCompanion;

/// The factory's rules over the record, without any reading: whose turn a job is, what waits for a person, and what
/// an answer is called. They mirror the app's (workStage.ts, needsYou.ts, factoryAnswer.ts); the hub decides.
public static class Factory
{
    public const int AnswerMax = 2000; // factory/answers.py MAX

    /// A tolerant read: every value here was written by somebody else.
    public static string? S(JsonNode? doc, string field) =>
        (doc as JsonObject)?["payload"] is JsonObject p && p[field] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public static int N(JsonNode? doc, string field) =>
        (doc as JsonObject)?["payload"] is JsonObject p && p[field] is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0;

    public static bool B(JsonNode? doc, string field) =>
        (doc as JsonObject)?["payload"] is JsonObject p && p[field] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public static string Key(JsonNode? doc) => (doc as JsonObject)?["key"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    /// By key; a row a page boundary showed twice counts once.
    public static Dictionary<string, JsonObject> ByKey(JsonArray docs) =>
        docs.OfType<JsonObject>().GroupBy(Key).ToDictionary(g => g.Key, g => g.Last());

    public static string Clip(string? text, int max)
    {
        var t = text ?? "";
        if (t.Length <= max) return t;
        var end = max > 0 && char.IsHighSurrogate(t[max - 1]) ? max - 1 : max;
        return t[..end] + "…";
    }

    public static string OneLine(string? text, int max) => Clip(Regex.Replace(text ?? "", @"\s+", " ").Trim(), max);

    public static bool Over(string? taskStage) => taskStage is "merged" or "cancelled";

    public static bool Waits(JsonNode? job) => S(job, "stage") is "needs-info" or "needs-human";

    /// A job that stopped before its first build takes no answer: a lead resumes it on the server.
    public static bool BeforeFirstBuild(JsonNode? job) => S(job, "stage") == "needs-human" && N(job, "round") < 1;

    public static bool IsDevice(JsonNode device, string? named) => named is not null && (S(device, "name") == named || Key(device) == named);

    public static bool Online(JsonNode device, DateTime now) =>
        S(device, "status") != "revoked" && Blocking.WithinHeartbeats(S(device, "lastSeen"), now);

    /// A run is going until the record says it ended.
    public static bool Going(JsonNode run) => S(run, "status") == "running" && string.IsNullOrEmpty(S(run, "endedAt"));

    /// Whose turn it is, in words (turnOf in workStage.ts).
    public static string WaitingFor(JsonNode? task, JsonNode job, Func<string, string> nameOf)
    {
        var assignee = S(task, "assignee");
        var person = string.IsNullOrEmpty(assignee) ? "a lead" : nameOf(assignee);
        if (Over(S(task, "stage"))) return "nobody";
        switch (S(job, "stage"))
        {
            case "merged" or "rejected": return S(task, "stage") == "review" ? "a lead: to review and merge" : "nobody";
            case "needs-info": return $"{person}: a question to answer";
            case "needs-human": return BeforeFirstBuild(job) ? "a lead: to resume it on the server" : $"{person}: to decide";
        }
        if (S(task, "stage") == "running") return "an agent, working";
        if (B(task, "checksFailed")) return $"{person}: its checks failed";
        if (string.IsNullOrEmpty(assignee)) return "a lead: it has no person";
        if (S(job, "stage") == "build")
            return string.IsNullOrEmpty(S(task, "device")) ? $"{person}: it has no computer" : $"{person}: its approval or its computer";
        return "the factory";
    }

    /// What the job asks: the body of the artifact that stopped it, below its header lines.
    public static string QuestionOf(IEnumerable<JsonNode> moves, string? stage)
    {
        var last = moves.Where(m => S(m, "toStage") == stage).OrderByDescending(m => N(m, "n")).FirstOrDefault();
        var text = (S(last, "text") ?? "").Trim();
        var at = Regex.Match(text, @"\n\s*\n");
        return at.Success ? text[at.Index..].Trim() : "";
    }

    public static string AnswerPrefix(string job, int moves) => $"decide-answer-{job}-m{moves}-";

    public static bool IsAnswerKey(string key) => Regex.IsMatch(key, @"\Adecide-answer-.+-m[0-9]+-[a-z0-9]{4,20}\z");

    /// What the hub said of one answer: null while it has not, "" when used, else why it was not.
    public static string? Verdict(JsonNode job, string messageKey)
    {
        try
        {
            if (JsonNode.Parse(S(job, "state") ?? "{}") is not JsonObject state || state["answered"] is not JsonArray all) return null;
            foreach (var x in all)
                if (x is JsonArray { Count: > 1 } pair && pair[0] is JsonValue k && k.TryGetValue<string>(out var key) && key == messageKey
                    && pair[1] is JsonValue w && w.TryGetValue<string>(out var why))
                    return why;
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            // not readable as that: the hub's word is not known
        }
        return null;
    }

    public sealed record Need(string Kind, string? Project, string Title, string Why, string Key, string WaitsFor, string Since);

    public sealed record Facts(string Me, bool IsLead, bool Signs, JsonArray Projects, JsonArray Tasks, JsonArray Jobs, JsonArray Proposals,
        JsonArray Baselines, JsonArray Guardrails, JsonArray Devices, ISet<string> Unsent, Func<string, string> NameOf, DateTime Now);

    private static readonly string[] Order =
        ["answer", "decide", "accept", "approve", "checks", "proposal", "sign", "machine", "review", "staff", "setup"];

    /// needsYou.ts: what needs a person across the projects, mine and the others', and what is not urgent.
    public static (List<Need> Mine, List<Need> Waiting, List<Need> Later) Needs(Facts f)
    {
        List<Need> mine = [], waiting = [], later = [];
        var active = ByKey(f.Projects).Where(p => S(p.Value, "status") != "archived")
            .ToDictionary(p => p.Key, p => S(p.Value, "name") ?? p.Key);
        var jobs = new Dictionary<string, JsonNode>();
        foreach (var j in f.Jobs.OfType<JsonObject>()) jobs[S(j, "task") ?? ""] = j;

        void Put(Need need, string who)
        {
            var me = who == "lead" ? f.IsLead : who == "signer" ? f.Signs : who == f.Me;
            if (me) mine.Add(need with { WaitsFor = "you" });
            else waiting.Add(need with { WaitsFor = who is "lead" or "signer" ? "a lead" : f.NameOf(who) });
        }

        string? OldRules(JsonNode t)
        {
            var now = S(f.Projects.FirstOrDefault(p => Key(p) == S(t, "project")), "currentBaseline");
            var planned = S(f.Baselines.FirstOrDefault(b => Key(b) == S(t, "baseline")), "version");
            return !string.IsNullOrEmpty(now) && !string.IsNullOrEmpty(planned) && planned != now ? now : null;
        }

        foreach (var t in f.Tasks.OfType<JsonObject>())
        {
            var project = S(t, "project") ?? "";
            if (!active.ContainsKey(project) || Over(S(t, "stage"))) continue;
            jobs.TryGetValue(Key(t), out var j);
            var assignee = S(t, "assignee");
            var has = !string.IsNullOrEmpty(assignee);
            var who = has ? assignee! : "lead";
            var rules = OldRules(t);
            void One(string kind, string why, string to) =>
                Put(new Need(kind, project, $"{S(t, "code")} {S(t, "title")}", why, Key(t), "", S(j, "updatedAt") ?? ""), to);
            // One card per task: the first thing that is true.
            if (S(j, "stage") == "needs-info") One("answer", "The factory has a question before it can build this.", who);
            else if (BeforeFirstBuild(j)) One("decide", $"It stopped before its first build: a lead resumes it on the server. {OneLine(S(j, "escalated"), 180)}".Trim(), "lead");
            else if (S(j, "stage") == "needs-human") One("decide", OneLine(S(j, "escalated"), 180) is { Length: > 0 } e ? e : "The factory stopped and needs a person.", who);
            else if (rules is not null && has) One("accept", $"The rules changed since it was planned (now {rules}). No run starts until they are accepted.", assignee!);
            else if (j is not null && f.Unsent.Contains(Key(j)) && has && S(t, "stage") != "running") One("approve", "The plan is ready. Its person's approval, in the app, starts the build.", assignee!);
            else if (B(t, "checksFailed") && has) One("checks", S(t, "note") is { Length: > 0 } n ? $"Checks failed: {OneLine(n, 180)}" : "The agent finished, but its checks failed.", assignee!);
            else if (S(t, "stage") == "review" && (j is null || S(j, "stage") == "rejected")) One("review", "Built and checked. A lead reviews it and merges it, in the app.", "signer");
        }
        foreach (var x in f.Proposals.OfType<JsonObject>().Where(x => S(x, "status") == "proposed" && S(x, "tool") != "models"))
            Put(new Need("proposal", null, S(x, "title") ?? "", "Curator proposes this. Nothing changes until a lead confirms it in the app.", Key(x), "", S(x, "createdAt") ?? ""), "lead");
        foreach (var b in f.Baselines.OfType<JsonObject>().Where(b => S(b, "status") == "draft" && active.ContainsKey(S(b, "project") ?? "")))
            Put(new Need("sign", S(b, "project"), $"Rules {S(b, "version")} of {active[S(b, "project")!]}", "A draft is ready to read and sign, in the app.", Key(b), "", ""), "signer");

        var paired = f.Devices.OfType<JsonObject>().Where(d => S(d, "owner") == f.Me && S(d, "status") != "revoked").ToList();
        var held = f.Tasks.OfType<JsonObject>().Where(t => S(t, "assignee") == f.Me && active.ContainsKey(S(t, "project") ?? "")
            && !Over(S(t, "stage")) && S(t, "stage") != "review").ToList();
        bool Named(JsonNode t) => !string.IsNullOrEmpty(S(t, "device"));
        bool Has(JsonNode t) => !Named(t) || paired.Any(d => IsDevice(d, S(t, "device")));
        var stuck = held.Where(t => Has(t) && !paired.Any(d => Online(d, f.Now) && (!Named(t) || IsDevice(d, S(t, "device"))))).ToList();
        if (held.Count > 0 && paired.Count == 0)
            mine.Add(new Need("machine", null, "Set up a computer", $"{held.Count} of your tasks can only run on one.", "machine", "you", ""));
        else if (stuck.Count > 0)
            mine.Add(new Need("machine", null, "A computer of yours is offline", $"{stuck.Count} of your tasks can only run on it.", "machine", "you", ""));
        foreach (var t in paired.Count > 0 ? held.Where(x => !Has(x)) : [])
            mine.Add(new Need("machine", S(t, "project"), $"{S(t, "code")} {S(t, "title")}", "It names a computer that is not one of yours now.", Key(t), "you", ""));

        if (f.IsLead)
        {
            var open = f.Tasks.OfType<JsonObject>().Where(t => active.ContainsKey(S(t, "project") ?? "") && !Over(S(t, "stage")) && string.IsNullOrEmpty(S(t, "assignee"))).ToList();
            if (open.Count > 0)
            {
                var most = open.GroupBy(t => S(t, "project")!).OrderByDescending(g => g.Count()).ToList();
                later.Add(new Need("staff", most[0].Key, $"{open.Count} task(s) with no person",
                    string.Join(", ", most.Take(3).Select(g => $"{active[g.Key]}: {g.Count()}")), "staff", "you", ""));
            }
            var withRules = f.Guardrails.Select(g => S(g, "baseline")).ToHashSet();
            var bare = active.Where(p =>
            {
                var version = S(f.Projects.First(x => Key(x) == p.Key), "currentBaseline");
                var b = f.Baselines.FirstOrDefault(x => S(x, "project") == p.Key && (string.IsNullOrEmpty(version) || S(x, "version") == version));
                return b is null || !withRules.Contains(Key(b));
            }).ToList();
            if (bare.Count > 0)
                later.Add(new Need("setup", bare[0].Key, $"{bare.Count} project(s) not set up",
                    string.Join(", ", bare.Take(4).Select(p => p.Value)) + ": no rules or safety limits yet.", "setup", "you", ""));
        }
        List<Need> Sorted(List<Need> all) => all.OrderBy(n => Array.IndexOf(Order, n.Kind)).ThenBy(n => n.Since, StringComparer.Ordinal)
            .ThenBy(n => n.Title, StringComparer.Ordinal).ToList();
        return (Sorted(mine), Sorted(waiting), later);
    }
}
