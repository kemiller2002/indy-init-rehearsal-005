// The .NET WebAssembly exports for the F# application shell: forward one serialized Limen message and return the
// serialized response. Marshalling only.

using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

return;

[SupportedOSPlatform("browser")]
public static partial class CausaEngine
{
    [JSExport]
    internal static string Dispatch(string messageJson) => Causa.App.Dispatch.handle(messageJson);

    /// Called only by the test build's main.js (scripts/build-site.mjs --test): accepts the Percepta state event.
    [JSExport]
    internal static void EnableTestHooks() => Causa.App.Dispatch.enableTestHooks();
}
