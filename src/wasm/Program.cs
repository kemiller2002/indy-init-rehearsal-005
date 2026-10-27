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

#if CAUSA_TEST_BUILD
    /// Test build only (scripts/build-site.mjs --test publishes with -p:CausaTestBuild=true): accepts the Percepta
    /// state event and fixture mode. The production assembly has no such export, so nothing in a deployed page can
    /// turn the hooks on.
    [JSExport]
    internal static void EnableTestHooks() => Causa.App.Dispatch.enableTestHooks();
#endif
}
