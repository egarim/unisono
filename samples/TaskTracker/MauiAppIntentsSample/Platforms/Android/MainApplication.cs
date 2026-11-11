using Android.App;
using Android.Runtime;
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

		// One registry, one more front door: the in-app MCP server on the phone's loopback.
		// From a Mac: `adb forward tcp:8787 tcp:8787`, then talk to http://127.0.0.1:8787/mcp.
		StartMcpServer();
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
