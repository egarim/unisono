namespace Unisono;

/// <summary>
/// Optional Siri phrases for a tool, used by the Swift generator for the App Shortcut it creates. Siri matches App
/// Shortcut phrases nearly literally, so list the ways people actually say it: "Where is my order", "Check my order",
/// "Track my order". Tokens: <c>{app}</c> becomes the app name (appended as " in {app}" when a phrase has none) and
/// <c>{parameterName}</c> becomes that parameter, which lets a phrase carry an enum or an entity ("Order {pupusaId}").
/// Without this attribute the generator uses "&lt;tool title&gt; in {app}". Ignored when the app declares its own
/// <c>AppShortcutsProvider</c> in Swift.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class UnisonoPhrasesAttribute(params string[] phrases) : Attribute
{
    public string[] Phrases { get; } = phrases;

    /// <summary>Short title shown on the App Shortcut tile; defaults to the tool title.</summary>
    public string? ShortTitle { get; set; }

    /// <summary>SF Symbol name for the tile, e.g. "flame"; defaults to one picked from the title's verb.</summary>
    public string? SystemImage { get; set; }
}
