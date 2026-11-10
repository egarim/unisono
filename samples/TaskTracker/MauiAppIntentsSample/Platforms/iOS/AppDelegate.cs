using Foundation;
using UIKit;
using MauiAppIntentsSample.Binding;
using MauiAppIntentsSample.Platforms.iOS;
using Unisono;
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
				TaskBridgeManager.Shared.Provider = new AppIntentsBridgeProvider(registry);
				Console.WriteLine($"[Unisono] Siri bridge wired to tools: {string.Join(", ", registry.Names)}");
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
