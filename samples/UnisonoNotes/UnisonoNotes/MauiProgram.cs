using Microsoft.Extensions.Logging;
using Unisono;
using Unisono.Mcp;
using UnisonoNotes.Pages;
using UnisonoNotes.Services;
using UnisonoNotes.Tools;

namespace UnisonoNotes;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<INoteService, NoteService>();

		// Unisono: the tool class, the registry built from it, and the in-app MCP server (port 8789;
		// TaskTracker uses 8787 so both can run side by side for the cross-app demo).
		builder.Services.AddSingleton<NoteTools>();
		builder.Services.AddSingleton(sp => new ToolRegistry()
			.AddToolsFrom(sp.GetRequiredService<NoteTools>())
			.AddEntityQuery("Note", new NoteEntityQuery(sp.GetRequiredService<INoteService>())));
		builder.Services.AddSingleton(sp => new UnisonoMcpHttpHost(
			sp.GetRequiredService<ToolRegistry>(),
			new UnisonoMcpHttpOptions
			{
				Port = 8789,
				ServerName = "unisono-notes",
				Instructions = "Unisono Notes holds short notes. Use list_notes to find a note id, get_note for the full body, extract_action_items for its open to-dos, and append_to_note to record what you did with them.",
			},
			sp.GetService<ILoggerFactory>()));

		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddTransient<NoteDetailPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
