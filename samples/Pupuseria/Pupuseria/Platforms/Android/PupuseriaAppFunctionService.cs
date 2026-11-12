using Android.App;
using Unisono.AppFunctions;

namespace Pupuseria;

/// <summary>The AppFunction service the system binds; the manifest declares it in full and names the XML asset.</summary>
[Service(Name = "com.unisono.samples.pupuseria.PupuseriaAppFunctionService")]
public class PupuseriaAppFunctionService : UnisonoAppFunctionService
{
}
