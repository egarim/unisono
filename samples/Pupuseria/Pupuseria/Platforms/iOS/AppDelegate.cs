using Foundation;
using UIKit;
using Unisono;
using Unisono.AppIntents;
using Unisono.Mcp;

namespace Pupuseria;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
	{
		var result = base.FinishedLaunching(application, launchOptions);
		UnisonoStartup.Wire();
		return result;
	}
}
