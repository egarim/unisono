using Android.App;
using Android.Runtime;
using Unisono;
using Unisono.AppFunctions;
using Unisono.Mcp;

namespace Pupuseria;

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
		// MCP server on the phone's loopback (from a Mac: `adb forward tcp:8790 tcp:8790`).
		var services = IPlatformApplication.Current?.Services;
		try
		{
			if (services?.GetService<ToolRegistry>() is { } registry)
			{
				UnisonoAppFunctions.Attach(registry);
				Android.Util.Log.Info("Unisono", $"AppFunctions attached. Tools: {string.Join(", ", registry.Names)}");
			}
			var host = services?.GetService<UnisonoMcpHttpHost>();
			host?.Start();
			Android.Util.Log.Info("Unisono", $"MCP server at {host?.Url}");
		}
		catch (Exception ex)
		{
			Android.Util.Log.Error("Unisono", $"ERROR during startup wiring: {ex}");
		}
	}
}
