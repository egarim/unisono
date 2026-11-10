using System.Text.RegularExpressions;

namespace UnisonoNotes.Models;

public class Note
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>First line of the body that is not an action item, for list previews.</summary>
    public string Preview => Body.Split('\n').Select(l => l.Trim())
        .FirstOrDefault(l => l.Length > 0 && !ActionItems.IsActionItem(l)) ?? string.Empty;

    public IReadOnlyList<string> OpenActionItems => ActionItems.Extract(Body);

    public string Summary
    {
        get
        {
            var n = OpenActionItems.Count;
            var items = n == 0 ? "no open action items" : n == 1 ? "1 open action item" : $"{n} open action items";
            return $"{items} · {CreatedAt.ToLocalTime():MMM d}";
        }
    }
}

/// <summary>
/// The deterministic rule for action items, shared by the tool, the UI and the Shortcuts flow:
/// an unchecked checkbox line ("- [ ] …", "* [ ] …") or a line starting with "TODO:" or "Action:".
/// </summary>
public static partial class ActionItems
{
    [GeneratedRegex(@"^\s*(?:[-*]\s*\[\s\]\s*|(?:TODO|Action)\s*:\s*)(.+?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ItemLine();

    public static bool IsActionItem(string line) => ItemLine().IsMatch(line);

    public static IReadOnlyList<string> Extract(string body)
        => body.Split('\n').Select(l => ItemLine().Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();
}
