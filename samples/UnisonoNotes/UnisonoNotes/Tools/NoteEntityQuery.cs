using Unisono;
using UnisonoNotes.Services;

namespace UnisonoNotes.Tools;

/// <summary>How Siri and Shortcuts resolve a "Note" entity: by id, by text, or from a suggested list.</summary>
public sealed class NoteEntityQuery(INoteService notes) : IEntityQuery<NoteDto>
{
    public ValueTask<IReadOnlyList<NoteDto>> ByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
        => new(ids.Select(notes.GetById).Where(n => n is not null).Select(n => NoteDto.From(n!)).ToList());

    public ValueTask<IReadOnlyList<NoteDto>> SearchAsync(string text, CancellationToken cancellationToken = default)
        => new(notes.Search(text).Select(NoteDto.From).ToList());

    public ValueTask<IReadOnlyList<NoteDto>> SuggestedAsync(CancellationToken cancellationToken = default)
        => new(notes.GetAll().Select(NoteDto.From).ToList());
}
