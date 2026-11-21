#if __WASM__
using System.Runtime.InteropServices.JavaScript;

namespace UnoPupusas;

/// <summary>
/// .NET browser WASM interop; async JS Promise is marshalled into Task.
/// Add the compiled unisono-mcp-bridge.js to the Uno bootstrap HTML.
/// </summary>
internal static partial class BrowserMcpBridge
{
    [JSImport("globalThis.UnisonoMcpBridge.calculatePupusas")]
    internal static partial Task<double> CalculatePupusasAsync(int quantity, double priceEach);
}
#endif
