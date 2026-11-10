using UnisonoNotes.Models;
using UnisonoNotes.Pages;
using UnisonoNotes.Services;

namespace UnisonoNotes;

public partial class MainPage : ContentPage
{
    private readonly INoteService _notes;

    public MainPage(INoteService notes)
    {
        InitializeComponent();
        _notes = notes;
        _notes.NotesChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Reload);
        Reload();
    }

    private void Reload() => NotesView.ItemsSource = _notes.GetAll();

    private async void OnNoteSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Note note)
            return;
        NotesView.SelectedItem = null;
        await Shell.Current.GoToAsync(nameof(NoteDetailPage), new Dictionary<string, object> { ["noteId"] = note.Id });
    }
}
