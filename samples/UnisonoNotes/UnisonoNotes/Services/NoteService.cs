using UnisonoNotes.Models;

namespace UnisonoNotes.Services;

/// <summary>In-memory notes, seeded with a standup note so the demo has action items to extract.</summary>
public class NoteService : INoteService
{
    private readonly List<Note> _notes = [];
    private readonly object _lock = new();

    public event EventHandler? NotesChanged;

    public NoteService() => Seed();

    public IReadOnlyList<Note> GetAll()
    {
        lock (_lock) return _notes.OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt).ToList();
    }

    public Note? GetById(string id)
    {
        lock (_lock) return _notes.FirstOrDefault(n => n.Id == id);
    }

    public IReadOnlyList<Note> Search(string text)
    {
        lock (_lock)
            return _notes.Where(n => n.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                                  || n.Body.Contains(text, StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt).ToList();
    }

    public Note Create(string title, string? body)
    {
        var note = new Note { Title = title, Body = body ?? string.Empty };
        lock (_lock) _notes.Add(note);
        NotesChanged?.Invoke(this, EventArgs.Empty);
        return note;
    }

    public Note? Append(string id, string text)
    {
        Note? note;
        lock (_lock)
        {
            note = _notes.FirstOrDefault(n => n.Id == id);
            if (note is null) return null;
            note.Body = note.Body.TrimEnd('\n') + "\n" + text;
            note.UpdatedAt = DateTime.UtcNow;
        }
        NotesChanged?.Invoke(this, EventArgs.Empty);
        return note;
    }

    private void Seed()
    {
        var today = DateTime.UtcNow;
        _notes.AddRange([
            new Note
            {
                Title = "Standup, Monday",
                CreatedAt = today.AddHours(-2),
                Body = """
                    Daily standup.

                    Yesterday: shipped the generic bridge; Siri and MCP now share one handler.
                    Today: start the Notes demo app.

                    Action items:
                    - [ ] Send the Q4 report to finance
                    - [ ] Book the dentist for next week
                    - [ ] Review Ana's pull request on the bridge
                    - [x] Rename the project to Unisono

                    Decision: ship v1.2 on Friday.
                    """,
            },
            new Note
            {
                Title = "Grocery list",
                CreatedAt = today.AddDays(-1),
                Body = """
                    For the weekend.
                    - [ ] Oat milk
                    - [ ] Coffee beans
                    - [ ] Rice
                    """,
            },
            new Note
            {
                Title = "Ideas for the demo",
                CreatedAt = today.AddDays(-2),
                Body = """
                    Show the same action from Shortcuts, from Claude and from the app itself.
                    Keep every scenario under two minutes.
                    """,
            },
        ]);
    }
}
