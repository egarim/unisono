using Microsoft.Extensions.Logging;
using MauiAppIntentsSample.Services;
using MauiAppIntentsSample.Tools;
using MauiAppIntentsSample.ViewModels;
using MauiAppIntentsSample.Views;
using Unisono;
using Unisono.Mcp;

namespace MauiAppIntentsSample;

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

		// Services
		builder.Services.AddSingleton<ITaskService, TaskService>();
#if IOS
		builder.Services.AddSingleton<IIntentDonationService, Platforms.iOS.IntentDonationService>();
#else
		builder.Services.AddSingleton<IIntentDonationService, NoOpIntentDonationService>();
#endif

		// Unisono: the tool class, the registry built from it, and the in-app MCP server.
		builder.Services.AddSingleton<TaskTools>();
		builder.Services.AddSingleton(sp => new ToolRegistry()
			.AddToolsFrom(sp.GetRequiredService<TaskTools>())
			.AddEntityQuery("Task", new TaskEntityQuery(sp.GetRequiredService<ITaskService>())));
		builder.Services.AddSingleton(sp => new UnisonoMcpHttpHost(
			sp.GetRequiredService<ToolRegistry>(),
			new UnisonoMcpHttpOptions
			{
				Port = 8787,
				ServerName = "tasktracker",
				Instructions = "TaskTracker is a to-do app. Use list_tasks or search_tasks to find a task id before completing it or changing its due date.",
			},
			sp.GetService<ILoggerFactory>()));

		// ViewModels
		builder.Services.AddSingleton<TaskListViewModel>();
		builder.Services.AddTransient<TaskDetailViewModel>();

		// Views
		builder.Services.AddSingleton<TaskListPage>();
		builder.Services.AddTransient<TaskDetailPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
