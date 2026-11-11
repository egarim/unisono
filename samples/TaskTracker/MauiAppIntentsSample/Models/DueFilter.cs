namespace MauiAppIntentsSample.Models;

/// <summary>A due-date window for listing tasks. Serialized camelCase ("thisWeek") for MCP and the Siri bridge.</summary>
public enum DueFilter
{
    Any,
    Today,
    Tomorrow,
    ThisWeek,
    Overdue,
}
