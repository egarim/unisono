using Microsoft.Extensions.Logging;
using Pupuseria.Pages;
using Pupuseria.Services;
using Pupuseria.Tools;
using Unisono;
using Unisono.Mcp;

namespace Pupuseria;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Fredoka-SemiBold.ttf", "FredokaSemiBold");
				fonts.AddFont("Fredoka-Bold.ttf", "FredokaBold");
				fonts.AddFont("Nunito-Regular.ttf", "Nunito");
				fonts.AddFont("Nunito-Bold.ttf", "NunitoBold");
				fonts.AddFont("Nunito-ExtraBold.ttf", "NunitoExtraBold");
			});

		// The kitchen: menu, cart, orders, the comal timer and Botilio's commentary.
		builder.Services.AddSingleton<IOrderService, OrderService>();

		// Unisono: the tool class, the registry built from it, and the in-app MCP server (port 8790;
		// TaskTracker uses 8787 and Notes 8789, so all three can run side by side).
		builder.Services.AddSingleton<PupuseriaTools>();
		builder.Services.AddSingleton(sp => new ToolRegistry()
			.AddToolsFrom(sp.GetRequiredService<PupuseriaTools>())
			.AddEntityQuery("Pupusa", new PupusaEntityQuery(sp.GetRequiredService<IOrderService>())));
		builder.Services.AddSingleton(sp => new UnisonoMcpHttpHost(
			sp.GetRequiredService<ToolRegistry>(),
			new UnisonoMcpHttpOptions
			{
				Port = 8790,
				ServerName = "pupuseria",
				Instructions = "Pupusería.net sells pupusas, the national dish of El Salvador. Use list_menu to see what there is, add_to_order or build_pupusa to fill the cart, place_order to send it to the comal, and get_order_status to hear how it is going. Botilio, the robot pupusero, answers everything.",
			},
			sp.GetService<ILoggerFactory>()));

		builder.Services.AddSingleton<MenuPage>();
		builder.Services.AddSingleton<BuildPage>();
		builder.Services.AddSingleton<CartPage>();
		builder.Services.AddSingleton<StatusPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
