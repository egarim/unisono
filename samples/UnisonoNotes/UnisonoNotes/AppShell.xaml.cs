using UnisonoNotes.Pages;

namespace UnisonoNotes;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(NoteDetailPage), typeof(NoteDetailPage));
	}
}
