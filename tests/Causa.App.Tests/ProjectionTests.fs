/// The shell's projection, without a browser: what is shown comes only from the port's projection.
module Causa.App.Tests.ProjectionTests

open System
open Xunit
open Limen.Contract.Core
open Causa.App
open Causa.App.Adapter

let private entropy = { Now = DateTime(2026, 10, 27); Ids = List.init 4 (fun _ -> Guid.NewGuid()); Nonce = "n" }

let private textOf (view: ViewState) key =
    match view |> Map.tryFind key with
    | Some(ViewValue.Text t) -> t
    | other -> failwithf "%s is %A" key other

let private flagOf (view: ViewState) key =
    match view |> Map.tryFind key with
    | Some(ViewValue.Flag b) -> b
    | other -> failwithf "%s is %A" key other

let private itemsOf (view: ViewState) key =
    match view |> Map.tryFind key with
    | Some(ViewValue.Items xs) -> xs
    | other -> failwithf "%s is %A" key other

let private field (item: ViewItem) key =
    match item |> Map.tryFind key with
    | Some(ViewPrimitive.Text t) -> t
    | other -> failwithf "%s is %A" key other

let private injected predicates caps =
    match TestState.parse $"""{{"name":"t","predicates":[{predicates}],"capabilities":{{{caps}}}}}""" with
    | Ok s -> TestState.toProjection s
    | Error e -> failwith e

let private at route port = { Shell.initial with Route = route; Port = port }

[<Fact>]
let ``VPO-2f4f9de66ddd6434 before Connect only the empty, disconnected workspace with Connect is shown`` () =
    let view = View.project (at Routes.Investigations Port.unavailable)
    Assert.True(flagOf view "screen.connect")
    Assert.False(flagOf view "screen.list")
    Assert.Empty(itemsOf view "cards")
    Assert.Equal("Not connected", textOf view "connection.label")

[<Fact>]
let ``VPO-be99a964783c59b5 VPO-9659118aa1ef95ff only the engine's capabilities are rendered, illegal ones with the engine's reason`` () =
    let ws = injected "\"material-blocker-exists\"" "\"RecordObservation\":\"legal\",\"ConfirmCause\":\"illegal\""
    let view = View.project (at (Routes.Investigation(TestState.fixtureId, Routes.Overview)) (Port.fixedAt ws))
    let caps = itemsOf view "capabilities"
    Assert.Equal<string list>([ "RecordObservation"; "ConfirmCause" ], caps |> List.map (fun c -> field c "id"))
    let confirm = caps |> List.find (fun c -> field c "id" = "ConfirmCause")
    Assert.Equal("An open material question must be answered first.", field confirm "reason")
    Assert.Equal(Some(ViewPrimitive.Flag false), confirm |> Map.tryFind "legal")

[<Fact>]
let ``VPO-56201f80b312c7c8 unknown counts are never shown as zero`` () =
    let ws = injected "" ""
    let view = View.project (at Routes.Investigations (Port.fixedAt ws))
    let card = itemsOf view "cards" |> List.exactlyOne
    Assert.Equal("unresolved contradictions: not known", field card "contradictions")
    Assert.Equal("1 hypotheses", field card "hypotheses")

[<Fact>]
let ``VPO-b3efad6eb85360be only a confirmed remote sync is described as saved to GitHub`` () =
    for s in [ NoChanges; Unsaved 2; Saving; Conflict; SaveFailed "GitHub refused the token."; ReadOnly; MayHaveSaved "lost" ] do
        Assert.DoesNotContain("Saved to GitHub", Presentation.syncText s)
    Assert.StartsWith("Saved to GitHub", Presentation.syncText (RemotelySynced(Some "12:00")))

[<Fact>]
let ``VPO-d4a9c8fd05795e00 unmapped domain values are never echoed raw`` () =
    Assert.Equal("Not recorded", Presentation.label "someInternalEnum")
    Assert.Equal("Awaiting evidence", Presentation.label "awaitingEvidence")

[<Fact>]
let ``the production dispatcher refuses the Percepta state event`` () =
    Dispatch.reset ()
    let model = Shell.initial
    let ev: SemanticEvent = { Name = "percepta-state"; Key = None; Value = Some """{"name":"x","predicates":[],"capabilities":{}}"""; Checked = None; Values = None; Submitter = None }
    let next, _ = Dispatch.step entropy (BrowserToEngineMessage.Event ev) model
    Assert.Equal(None, next.InjectedState)
    Assert.Equal("engine/github", next.Port.Name)

[<Fact>]
let ``VPO-bb08b9a414ba4cd7 the token leaves the shell model when Connect is handed to the port`` () =
    let ev name value: SemanticEvent = { Name = name; Key = None; Value = value; Checked = None; Values = None; Submitter = None }
    let m, _ = Shell.update entropy (BrowserToEngineMessage.Event(ev "token" (Some "github_pat_secret"))) Shell.initial
    Assert.Equal("github_pat_secret", m.Drafts.Token)
    let m, _ = Shell.update entropy (BrowserToEngineMessage.Event(ev "connect" None)) m
    Assert.Equal("", m.Drafts.Token)
    let view = View.project m
    for KeyValue(_, v) in view do
        Assert.DoesNotContain("github_pat_secret", sprintf "%A" v)

[<Fact>]
let ``VPO-89ed6170f531961e an unknown save outcome is its own state: neither saved nor failed, with check now and no retry`` () =
    let s = MayHaveSaved "the answer was lost"
    Assert.Equal("Your changes may or may not have reached GitHub; checking the branch before anything else is sent.", Presentation.syncText s)
    Assert.Equal("unknown", Presentation.syncTone s)
    Assert.NotEqual<string>(Presentation.syncTone (RemotelySynced None), Presentation.syncTone s)
    Assert.NotEqual<string>(Presentation.syncTone (SaveFailed "x"), Presentation.syncTone s)
    let ws = { emptyWorkspace with Connection = ConnectedTo("a/b", "main", "w"); Sync = s }
    let view = View.project { Shell.initial with Route = Routes.Investigations; Port = Port.fixedAt ws }
    Assert.True(flagOf view "sync.mayHaveSaved")

/// A port that records every input it receives and changes nothing.
let private recording () =
    let seen = Collections.Generic.List<AdapterInput>()
    let projection = { emptyWorkspace with Connection = ConnectedTo("a/b", "main", "w"); Sync = Conflict; ConnectionStatus = Some(EnginePort.connectionWords EnginePort.ConnectionState.Conflict, None) }
    let rec make () : Port =
        { Name = "recording"
          History = fun _ _ -> None
          Projection = projection
          Handle = fun _ input -> seen.Add input; make (), [] }
    make (), seen

[<Fact>]
let ``VPO-34731ac9a977b818 on a conflict the shell offers reload and discard, and each reaches persistence for that investigation only`` () =
    let port, seen = recording ()
    let id = "00000000-0000-4000-8000-0000000000c1"
    let model = { Shell.initial with Route = Routes.Investigation(id, Routes.Overview); Port = port }
    let view = View.project model
    Assert.True(flagOf view "sync.conflict")
    Assert.Equal("Connected: someone else changed an investigation", textOf view "connection.label")
    let ev name: SemanticEvent = { Name = name; Key = None; Value = None; Checked = None; Values = None; Submitter = None }
    Shell.update entropy (BrowserToEngineMessage.Event(ev "reload-remote")) model |> ignore
    // Discarding local work waits for an explicit confirmation (UI-004-O3); cancelling sends nothing.
    let held, _ = Shell.update entropy (BrowserToEngineMessage.Event(ev "discard-local")) model
    Assert.Equal<AdapterInput list>([ ReloadRemote id ], List.ofSeq seen)
    Assert.True(flagOf (View.project held) "confirm.show")
    let cancelled, _ = Shell.update entropy (BrowserToEngineMessage.Event(ev "confirm-no")) held
    Assert.Equal<AdapterInput list>([ ReloadRemote id ], List.ofSeq seen)
    Assert.False(flagOf (View.project cancelled) "confirm.show")
    let held, _ = Shell.update entropy (BrowserToEngineMessage.Event(ev "discard-local")) model
    Shell.update entropy (BrowserToEngineMessage.Event(ev "confirm-yes")) held |> ignore
    Assert.Equal<AdapterInput list>([ ReloadRemote id; DiscardLocal id ], List.ofSeq seen)

[<Fact>]
let ``every connection state has its own words`` () =
    let states =
        [ EnginePort.ConnectionState.Disconnected; EnginePort.ConnectionState.Connecting; EnginePort.ConnectionState.Loading
          EnginePort.ConnectionState.AuthorizedEmpty; EnginePort.ConnectionState.Connected; EnginePort.ConnectionState.Incompatible
          EnginePort.ConnectionState.AccessDenied; EnginePort.ConnectionState.Offline; EnginePort.ConnectionState.Conflict ]
    let words = states |> List.map EnginePort.connectionWords
    Assert.Equal(states.Length, (List.distinct words).Length)

[<Fact>]
let ``VPO-014f5e1139ed692b a frozen demonstration and a writable copy are labelled apart in the catalog and the investigation header`` () =
    let card id writable = { Id = id; Title = "T " + id; Lifecycle = "active"; Writable = writable; LastActivity = None; Hypotheses = Known 0; Evidence = Known 0; Contradictions = Known 0; OpenQuestionCount = Known 0; NextAction = None }
    let ws = { emptyWorkspace with Connection = ConnectedTo("a/b", "main", "w"); Investigations = Loaded [ card "00000000-0000-4000-8000-00000000000a" false; card "00000000-0000-4000-8000-00000000000b" true ] }
    let view = View.project { Shell.initial with Route = Routes.Investigations; Port = Port.fixedAt ws }
    let kinds = itemsOf view "cards" |> List.map (fun c -> field c "kind")
    Assert.Equal<string list>([ "Read-only demonstration"; "Your working copy" ], kinds)
    let fixture = injected "" ""
    let readOnly = fixture.Opened |> Map.map (fun _ l -> match l with Loaded i -> Loaded { i with Writable = false } | o -> o)
    let opened = View.project { Shell.initial with Route = Routes.Investigation(TestState.fixtureId, Routes.Overview); Port = Port.fixedAt { fixture with Opened = readOnly } }
    Assert.Equal("Read-only demonstration", textOf opened "inv.kind")
