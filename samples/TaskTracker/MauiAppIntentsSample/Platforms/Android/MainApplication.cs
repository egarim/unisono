using Android.App;
using Android.Runtime;
using Unisono;
using Unisono.AppFunctions;
using Unisono.Mcp;

namespace MauiAppIntentsSample;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override void OnCreate()
	{
		base.OnCreate();

		// One registry, two front doors on Android: AppFunctions for Gemini and the platform tools, and the in-app
		// MCP server on the phone's loopback (from a Mac: `adb forward tcp:8787 tcp:8787`, then http://127.0.0.1:8787/mcp).
		AttachAppFunctions();
		StartMcpServer();
	}

	private static void AttachAppFunctions()
	{
		var registry = IPlatformApplication.Current?.Services.GetService<ToolRegistry>();
		if (registry is null)
		{
			Android.Util.Log.Warn("Unisono", "ToolRegistry not found in DI container; AppFunctions not attached.");
			return;
		}
		UnisonoAppFunctions.Attach(registry);
		Android.Util.Log.Info("Unisono", $"AppFunctions attached. Tools: {string.Join(", ", registry.Names)}");
	}

	private static void StartMcpServer()
	{
		try
		{
			var host = IPlatformApplication.Current?.Services.GetService<UnisonoMcpHttpHost>();
			host?.Start();
			Android.Util.Log.Info("Unisono", $"MCP server at {host?.Url}");
		}
		catch (Exception ex)
		{
			Android.Util.Log.Error("Unisono", $"ERROR starting MCP server: {ex}");
		}
	}
}
