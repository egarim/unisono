using UnisonoNotes.Services;

namespace UnisonoNotes.Pages;

[QueryProperty(nameof(NoteId), "noteId")]
public partial class NoteDetailPage : ContentPage
{
    private readonly INoteService _notes;

    public NoteDetailPage(INoteService notes)
    {
        InitializeComponent();
        _notes = notes;
        _notes.NotesChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Render);
    }

    public string? NoteId { get; set; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Render();
    }

    private void Render()
    {
        if (NoteId is null || _notes.GetById(NoteId) is not { } note)
            return;

        TitleLabel.Text = note.Title;
        MetaLabel.Text = note.UpdatedAt is { } u
            ? $"Created {note.CreatedAt.ToLocalTime():g} · updated {u.ToLocalTime():g}"
            : $"Created {note.CreatedAt.ToLocalTime():g}";
        BodyLabel.Text = note.Body;

        ItemsLayout.Children.Clear();
        var items = note.OpenActionItems;
        if (items.Count == 0)
            ItemsLayout.Children.Add(new Label { Text = "None", TextColor = Colors.Gray, FontSize = 14 });
        foreach (var item in items)
            ItemsLayout.Children.Add(new Label { Text = "☐ " + item, FontSize = 15 });
    }
}
