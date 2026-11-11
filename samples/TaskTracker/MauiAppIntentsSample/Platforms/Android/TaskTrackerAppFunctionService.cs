using Android.App;
using Unisono.AppFunctions;

namespace MauiAppIntentsSample;

/// <summary>
/// The AppFunction service the system binds for this app. Everything it does lives in the base class; the app only
/// owns the Java class name (the attribute) and the full manifest entry in Platforms/Android/AndroidManifest.xml:
/// exported, BIND_APP_FUNCTION_SERVICE permission, the intent filter and the android.app.appfunctions property that
/// points at the Assets/app_functions.xml asset.
/// </summary>
[Service(Name = "com.unisono.samples.tasktracker.TaskTrackerAppFunctionService")]
public class TaskTrackerAppFunctionService : UnisonoAppFunctionService
{
}
