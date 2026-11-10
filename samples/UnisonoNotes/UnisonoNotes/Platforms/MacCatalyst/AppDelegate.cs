using Foundation;
using UIKit;
using Unisono;
using Unisono.AppIntents;
using Unisono.Mcp;

namespace UnisonoNotes;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
	{
		var result = base.FinishedLaunching(application, launchOptions);

		// Same two front doors as on iOS. On the Mac the MCP endpoint stays up as long as the app runs,
		// which is what the two-app agent scenario needs.
		var services = IPlatformApplication.Current?.Services;
		try
		{
			if (services?.GetService<ToolRegistry>() is { } registry)
			{
				UnisonoAppIntents.Attach(registry);
				Console.WriteLine($"[Unisono] Siri bridge attached. Tools: {string.Join(", ", registry.Names)}. Entities: {string.Join(", ", registry.EntityTypes)}");
			}
			var host = services?.GetService<UnisonoMcpHttpHost>();
			host?.Start();
			Console.WriteLine($"[Unisono] MCP server at {host?.Url}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[Unisono] ERROR during startup wiring: {ex}");
		}

		return result;
	}
}
