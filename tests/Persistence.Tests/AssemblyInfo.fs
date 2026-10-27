namespace Persistence.Tests

open Xunit

/// The engine's schema validator caches compiled patterns in a non-concurrent Dictionary (src/engine/Schema.fs,
/// `regex`): two loads on different threads corrupt it. The browser runs single-threaded, so the application is
/// unaffected; tests run one at a time until core makes the cache thread-safe (reported to the coordinator).
[<assembly: CollectionBehavior(DisableTestParallelization = true)>]
do ()
