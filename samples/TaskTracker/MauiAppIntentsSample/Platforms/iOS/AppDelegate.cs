using Foundation;
using UIKit;
using Unisono;
using Unisono.AppIntents;
using Unisono.Mcp;

namespace MauiAppIntentsSample;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
	{
		var result = base.FinishedLaunching(application, launchOptions);

		// Two front doors, one registry: Siri through the Swift bridge, agents through MCP.
		WireUpAppIntentsBridge();
		StartMcpServer();

		return result;
	}

	private void WireUpAppIntentsBridge()
	{
		try
		{
			var registry = IPlatformApplication.Current?.Services.GetService<ToolRegistry>();
			if (registry is not null)
			{
				UnisonoAppIntents.Attach(registry);
				Console.WriteLine($"[Unisono] Siri bridge attached. Tools: {string.Join(", ", registry.Names)}. Entities: {string.Join(", ", registry.EntityTypes)}");
			}
			else
			{
				Console.WriteLine("[Unisono] WARNING: ToolRegistry not found in DI container.");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[Unisono] ERROR wiring Siri bridge: {ex.Message}");
		}
	}

	private void StartMcpServer()
	{
		try
		{
			var host = IPlatformApplication.Current?.Services.GetService<UnisonoMcpHttpHost>();
			host?.Start();
			Console.WriteLine($"[Unisono] MCP server at {host?.Url}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[Unisono] ERROR starting MCP server: {ex}");
		}
	}
}
