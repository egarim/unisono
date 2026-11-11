using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Android.App.AppFunctions;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Unisono;

namespace Unisono.AppFunctions;

/// <summary>
/// The Android service the system binds to execute an AppFunction. Subclass it in the app (so the app owns the
/// manifest entry and the Java class name) and declare it with
/// <c>[Service(Exported = true, Permission = "android.permission.BIND_APP_FUNCTION_SERVICE")]</c> plus an intent
/// filter for <c>android.app.appfunctions.AppFunctionService</c>. Each request's function id names a registry tool;
/// the request's <c>GenericDocument</c> parameters become the tool's JSON arguments and the tool's JSON result becomes
/// the response document, under the platform's <c>androidAppFunctionReturnValue</c> property.
/// </summary>
[SupportedOSPlatform("android36.0")]
public class UnisonoAppFunctionService : AppFunctionService
{
    public UnisonoAppFunctionService() { }

    protected UnisonoAppFunctionService(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }

    /// <summary>The registry to dispatch to; defaults to <see cref="UnisonoAppFunctions.Registry"/>.</summary>
    protected virtual ToolRegistry? Registry => UnisonoAppFunctions.Registry;

    public override void OnExecuteFunction(
        ExecuteAppFunctionRequest request,
        string callingPackage,
        SigningInfo callingPackageSigningInfo,
        CancellationSignal cancellationSignal,
        IOutcomeReceiver callback)
    {
        var registry = Registry;
        if (registry is null)
        {
            Fail(callback, AppFunctionError.SystemError,
                "Unisono: no ToolRegistry attached. Call UnisonoAppFunctions.Attach(registry) at application start.");
            return;
        }

        var toolName = UnisonoAppFunctions.ToolNameFromFunctionId(request.FunctionIdentifier);
        if (!registry.Names.Contains(toolName))
        {
            Fail(callback, AppFunctionError.FunctionNotFound,
                $"Unisono: no tool named '{toolName}'. Tools: {string.Join(", ", registry.Names)}");
            return;
        }

        JsonObject arguments;
        try
        {
            arguments = GenericDocumentJson.ToJsonObject(request.Parameters);
        }
        catch (Exception ex)
        {
            Fail(callback, AppFunctionError.InvalidArgument,
                $"Unisono: could not read the parameters of '{toolName}': {ex.Message}");
            return;
        }

        var cts = new CancellationTokenSource();
        cancellationSignal.SetOnCancelListener(new CancelListener(cts));

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await registry.InvokeAsync(toolName, arguments, cts.Token).ConfigureAwait(false);
                var document = GenericDocumentJson.ToResultDocument(result);
                callback.OnResult(new ExecuteAppFunctionResponse(document));
            }
            catch (System.OperationCanceledException)
            {
                Fail(callback, AppFunctionError.Cancelled, $"Unisono: '{toolName}' was cancelled.");
            }
            catch (Exception ex)
            {
                Fail(callback, AppFunctionError.AppUnknownError, $"Unisono: '{toolName}' failed: {ex.Message}");
            }
        });
    }

    /// <summary>Reports an <see cref="AppFunctionException"/> through the receiver (which takes a Java object, not a .NET exception).</summary>
    private static void Fail(IOutcomeReceiver callback, AppFunctionError code, string message)
    {
        using var exception = new AppFunctionException(code, message);
        var javaObject = Java.Lang.Object.GetObject<Java.Lang.Object>(exception.Handle, JniHandleOwnership.DoNotTransfer)!;
        callback.OnError(javaObject);
    }

    private sealed class CancelListener(CancellationTokenSource cts) : Java.Lang.Object, CancellationSignal.IOnCancelListener
    {
        public void OnCancel() => cts.Cancel();
    }
}
