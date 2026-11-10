using UnisonoNotes.Models;

namespace UnisonoNotes.Services;

public interface INoteService
{
    IReadOnlyList<Note> GetAll();
    Note? GetById(string id);
    IReadOnlyList<Note> Search(string text);
    Note Create(string title, string? body);
    Note? Append(string id, string text);
    event EventHandler? NotesChanged;
}
