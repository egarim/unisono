using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;

namespace Unisono.Samples.VivariumCompanion;

/// What the person of this machine lets the companion write: ~/.creatura/mcp-grants.json, for example
/// {"answer_job": true, "move_task": true}. Read again on every check, so taking a grant back counts at once. A file
/// that is missing, unreadable or not a JSON object, and any value but the literal true, allow nothing.
public sealed class Grants(string path)
{
    public const string AnswerJob = "answer_job", RecordDecision = "record_decision", MoveTask = "move_task";

    public static string DefaultPath(string home) => Path.Combine(home, ".creatura", "mcp-grants.json");

    public bool Allows(string grant)
    {
        // With no user profile the path would be relative: a file of whatever folder this runs in grants nothing.
        if (!Path.IsPathFullyQualified(path)) return false;
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(file);
            return JsonNode.Parse(reader.ReadToEnd()) is JsonObject all
                && all[grant] is JsonValue v && v.GetValueKind() == JsonValueKind.True;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException
                                      or ArgumentException or NotSupportedException)
        {
            return false; // includes a key written twice (ArgumentException): not a file that can be read one way
        }
    }

    public void Require(string grant)
    {
        if (!Allows(grant))
            throw new McpException($"not allowed: this computer's person has not granted {grant} (mcp-grants.json). " +
                                   "Ask them; do not change that file yourself.");
    }

    public void RequireDecision()
    {
        if (!Allows(RecordDecision))
            throw new McpException("not allowed: this computer's person lets you answer, not decide for them (record_decision in mcp-grants.json). " +
                                   "Leave `decisions` out, or let the person write the decision in the app.");
    }
}
