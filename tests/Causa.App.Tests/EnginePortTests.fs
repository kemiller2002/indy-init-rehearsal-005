/// T17 at the application level: typed commands cross the shell's Port into the core kernel (Boundary A) and every
/// view is re-projected from the one new version (Boundary B). Test names carry the obligation ids they prove.
module Causa.App.Tests.EnginePortTests

open System
open Xunit
open Limen.Contract.Core
open Causa.App
open Causa.App.Adapter

let private entropy () = { Now = DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc); Ids = List.init 4 (fun _ -> Guid.NewGuid()); Nonce = "n" }

let private memory () = EnginePort.create (EnginePort.Persistence.memory Map.empty [])

let private start (port: Port) =
    let port, fx = port.Handle (entropy ()) (Command(None, "StartInvestigation", Map.ofList [ "title", "Valve leak"; "problemStatement", "Coolant found under valve 3." ], 0L))
    let id =
        match port.Projection.Investigations with
        | Loaded [ c ] -> c.Id
        | other -> failwithf "expected one investigation, got %A" other
    port, fx, id

let private opened (port: Port) id =
    match port.Projection.Opened.TryFind id with
    | Some(Loaded inv) -> inv
    | other -> failwithf "not loaded: %A" other

[<Fact>]
let ``VPO-be99a964783c59b5 VPO-21abf6acd60d0acc a started investigation is projected by the engine at version 1 and opened`` () =
    let port, fx, id = start (memory ())
    let inv = opened port id
    Assert.Equal(1L, inv.Version)
    Assert.Equal("draft", inv.Lifecycle)
    Assert.Equal(Some "Coolant found under valve 3.", inv.Description)
    // The shell navigates to the new investigation's own address.
    Assert.Contains(fx, fun e -> match e with EffectRequest.Navigation _ -> true | _ -> false)
    // Capabilities are the engine's: activation is offered on a draft.
    Assert.Contains(inv.Capabilities, fun c -> c.Name = "ActivateInvestigation" && c.Legal)

[<Fact>]
let ``VPO-21abf6acd60d0acc a legal command produces events and every view refreshes from the one new version`` () =
    let port, _, id = start (memory ())
    let port, _ = port.Handle (entropy ()) (Command(Some id, "ActivateInvestigation", Map.empty, 1L))
    let inv = opened port id
    Assert.Equal(2L, inv.Version)
    Assert.Equal("active", inv.Lifecycle)
    let card = match port.Projection.Investigations with Loaded [ c ] -> c | _ -> failwith "card"
    Assert.Equal("active", card.Lifecycle)
    Assert.Equal(2, inv.Timeline.Length)
    Assert.Equal(Unsaved 2, port.Projection.Sync)

[<Fact>]
let ``VPO-be22751d9b5b8098 a stale-version submission is rejected and changes nothing`` () =
    let port, _, id = start (memory ())
    let port, _ = port.Handle (entropy ()) (Command(Some id, "ActivateInvestigation", Map.empty, 1L))
    let before = opened port id
    let port2, fx = port.Handle (entropy ()) (Command(Some id, "RecordHypothesis", Map.ofList [ "text", "A seal failed." ], 1L))
    Assert.Equal(before, opened port2 id)
    Assert.Empty(fx)
    match port2.Projection.Message with
    | Some("warning", text) -> Assert.StartsWith("Not recorded:", text)
    | other -> failwithf "%A" other

[<Fact>]
let ``VPO-9659118aa1ef95ff an illegal command is refused with the engine's reasons and nothing changes`` () =
    let port, _, id = start (memory ())
    // A draft cannot be resolved; the engine says why.
    let port2, _ = port.Handle (entropy ()) (Command(Some id, "ResolveInvestigation", Map.empty, 1L))
    Assert.Equal(1L, (opened port2 id).Version)
    match port2.Projection.Message with
    | Some("warning", text) -> Assert.True(text.Length > "Not recorded: ".Length)
    | other -> failwithf "%A" other

[<Fact>]
let ``VPO-b3efad6eb85360be the in-memory workspace never claims a save`` () =
    let port, _, _ = start (memory ())
    Assert.DoesNotContain("Saved to GitHub", Presentation.syncText port.Projection.Sync)
    match port.Projection.Message with
    | Some(_, text) -> Assert.Contains("Not saved yet", text)
    | None -> failwith "no message"

[<Fact>]
let ``VPO-2f4f9de66ddd6434 the production port refuses Connect and claims no connection`` () =
    let port = EnginePort.create EnginePort.Persistence.unavailable
    let port, fx = port.Handle (entropy ()) (Connect { Token = "t"; Repository = "a/b"; Branch = "main"; Workspace = "w" })
    Assert.Empty(fx)
    Assert.Equal(NotRequested, port.Projection.Investigations)
    match port.Projection.Connection with
    | ConnectionFailed _ -> ()
    | other -> failwithf "%A" other
