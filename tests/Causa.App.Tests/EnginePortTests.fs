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

[<Fact>]
let ``VPO-d0e33c8aaaf705f2 VPO-f0256bc31283be14 VPO-58231f4725864068 integrity that passed never becomes a Verified finding in the investigation views`` () =
    // A loaded investigation whose data passed every integrity check but holds no confirmed cause.
    let port, _, id = start (memory ())
    let inv = opened port id
    let ws = port.Projection
    let docs =
        ws.Opened
        |> Map.map (fun _ l ->
            match l with
            | Loaded i -> Loaded { i with Integrity = Some "events SHA-256 abc…, state SHA-256 def…, admissibility checked" }
            | other -> other)
    let model = { Shell.initial with Route = Routes.Investigation(id, Routes.Overview); Port = Port.fixedAt { ws with Opened = docs } }
    let view = View.project model
    let text k = match view |> Map.tryFind k with Some(ViewValue.Text t) -> t | _ -> ""
    Assert.Equal("Confirmed causes: 0 · Corrective actions verified effective: 0 · Not resolved", text "findings.text")
    // No investigation-facing value says Verified; the integrity fact appears only in Administration.
    for KeyValue(k, v) in view do
        if not (k.StartsWith "admin") then Assert.DoesNotContain("Verified", (sprintf "%A" v).Replace("verified effective", ""))
    let admin = View.project { model with Route = Routes.Administration }
    Assert.Contains("Integrity check passed", sprintf "%A" (admin |> Map.find "admin.persistence"))
    Assert.Equal(Some { ConfirmedCauses = 0; VerifiedCorrections = 0; Resolved = false }, inv.Findings)

[<Fact>]
let ``VPO-b8e1657130d006fd an investigation that failed to load renders no partial state`` () =
    let id = "00000000-0000-4000-8000-0000000000a9"
    let ws = { emptyWorkspace with Connection = ConnectedTo("a/b", "main", "w"); Investigations = Loaded []; Opened = Map.ofList [ id, Failed "The history failed validation at event 4; nothing from it is shown." ] }
    let view = View.project { Shell.initial with Route = Routes.Investigation(id, Routes.Overview); Port = Port.fixedAt ws }
    Assert.Equal(Some(ViewValue.Flag true), view |> Map.tryFind "screen.failed")
    Assert.Equal(Some(ViewValue.Flag false), view |> Map.tryFind "screen.investigation")
    Assert.Equal(Some(ViewValue.Items []), view |> Map.tryFind "hypotheses")
    Assert.Equal(Some(ViewValue.Text ""), view |> Map.tryFind "inv.title")

[<Fact>]
let ``VPO-f8666daf6fdf79d7 VPO-ffda9fea0324a42e the report explains only from Projection.report's typed records`` () =
    let port, _, id = start (memory ())
    let port, _ = port.Handle (entropy ()) (Command(Some id, "ActivateInvestigation", Map.empty, 1L))
    let port, _ = port.Handle (entropy ()) (Command(Some id, "IdentifyUnknown", Map.ofList [ "text", "Was the valve replaced last month?" ], 2L))
    let inv = opened port id
    let r = inv.Report.Value
    Assert.Equal(None, r.Conclusion)
    Assert.Equal<string list>([ "Was the valve replaced last month?" ], r.OpenQuestions)
    let view = View.project { Shell.initial with Route = Routes.Investigation(id, Routes.Report); Port = port }
    Assert.Equal(Some(ViewValue.Text "No cause has been confirmed and no conclusion is recorded. The report states no conclusion."), view |> Map.tryFind "report.conclusion")
    Assert.Contains("Open question: Was the valve replaced last month?", sprintf "%A" (view |> Map.find "report.open"))
