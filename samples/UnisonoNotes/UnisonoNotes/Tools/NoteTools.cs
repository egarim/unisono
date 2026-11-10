using System.ComponentModel;
using ModelContextProtocol.Server;
using UnisonoNotes.Models;
using UnisonoNotes.Services;

namespace UnisonoNotes.Tools;

/// <summary>
/// The one place Unisono Notes' actions are defined. Siri and Shortcuts reach them through the Swift
/// intents and the Unisono bridge; MCP clients through the in-app server. Descriptions are for models.
/// </summary>
[McpServerToolType]
public sealed class NoteTools(INoteService notes)
{
    [McpServerTool(Name = "create_note", Title = "Create Note", Destructive = false, Idempotent = false)]
    [Description("Creates a note and returns it. Lines in the body that start with '- [ ]' are open action items.")]
    public NoteDto CreateNote(
        [Description("Title of the note")] string title,
        [Description("Body text; Markdown is fine")] string? body = null)
        => NoteDto.From(notes.Create(title, body));

    [McpServerTool(Name = "list_notes", Title = "Find Notes", ReadOnly = true, Destructive = false)]
    [Description("Lists notes, most recently changed first. With a query, only notes whose title or body contain the text.")]
    public IReadOnlyList<NoteDto> ListNotes([Description("Text to look for; omit for all notes")] string? query = null)
        => (string.IsNullOrWhiteSpace(query) ? notes.GetAll() : notes.Search(query)).Select(NoteDto.From).ToList();

    [McpServerTool(Name = "get_note", Title = "Get Note", ReadOnly = true, Destructive = false)]
    [Description("Returns one note with its full body, or null if the id is unknown.")]
    public NoteDto? GetNote([Description("The note id")] string id)
        => notes.GetById(id) is { } note ? NoteDto.From(note) : null;

    [McpServerTool(Name = "append_to_note", Title = "Append to Note", Destructive = false, Idempotent = false)]
    [Description("Appends a line of text to the end of a note and returns the updated note.")]
    public NoteDto AppendToNote(
        [Description("The note id")] string id,
        [Description("Text to append on a new line")] string text)
        => NoteDto.From(notes.Append(id, text) ?? throw new KeyNotFoundException($"No note with id '{id}'."));

    [McpServerTool(Name = "extract_action_items", Title = "Extract Action Items", ReadOnly = true, Destructive = false)]
    [Description("Returns the open action items of a note: lines starting with '- [ ]', 'TODO:' or 'Action:'. Each one is a good title for a task in a task app.")]
    public IReadOnlyList<string> ExtractActionItems([Description("The note id")] string id)
        => (notes.GetById(id) ?? throw new KeyNotFoundException($"No note with id '{id}'.")).OpenActionItems;
}

/// <summary>Wire shape of a note, shared by MCP results and the Siri bridge.</summary>
public sealed record NoteDto(string Id, string Title, string Body, DateTime CreatedAt, DateTime? UpdatedAt, int OpenActionItems)
{
    public static NoteDto From(Note n) => new(n.Id, n.Title, n.Body, n.CreatedAt, n.UpdatedAt, n.OpenActionItems.Count);
}
