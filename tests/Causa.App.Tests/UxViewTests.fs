/// The catalog and inspector keys as the projected view (stream ux). Test names carry the obligation ids they prove.
module Causa.App.Tests.UxViewTests

open Xunit
open Limen.Contract.Core
open Causa.App
open Causa.App.Adapter

let private model (ws: WorkspaceProjection) route selected : Shell.Model =
    { Shell.initial with Port = Port.fixedAt ws; Route = route; Selected = selected }

let private connected = { emptyWorkspace with Connection = ConnectedTo("owner/repo", "main", "ws") }

let private textOf (v: Map<string, ViewValue>) key =
    match v.TryFind key with
    | Some(ViewValue.Text t) -> t
    | other -> failwithf "%s is %A" key other

let private flagOf (v: Map<string, ViewValue>) key =
    match v.TryFind key with
    | Some(ViewValue.Flag b) -> b
    | other -> failwithf "%s is %A" key other

let private card id title lifecycle activity : CardProjection =
    { Id = id; Title = title; Lifecycle = lifecycle; Writable = true; LastActivity = activity
      Hypotheses = Known 2; Evidence = Unknown; Contradictions = Known 0; OpenQuestionCount = Unknown; NextAction = None }

[<Fact>]
let ``VPO-1cd1f428fe646f06 the catalog has distinct loading, error and empty states`` () =
    let loading = View.project (model { connected with Investigations = Loading } Routes.Investigations "")
    Assert.True(flagOf loading "list.loading")
    Assert.False(flagOf loading "list.failed")
    Assert.False(flagOf loading "list.empty")
    let failed = View.project (model { connected with Investigations = Failed "The list could not be read. Nothing was changed." } Routes.Investigations "")
    Assert.True(flagOf failed "list.failed")
    Assert.Equal("The list could not be read. Nothing was changed.", textOf failed "list.failure")
    Assert.False(flagOf failed "list.loading")
    let empty = View.project (model { connected with Investigations = Loaded [] } Routes.Investigations "")
    Assert.True(flagOf empty "list.empty")
    Assert.False(flagOf empty "list.loading")

[<Fact>]
let ``VPO-1cd1f428fe646f06 cards sort by latest activity, title or status, and the selected card is marked`` () =
    let cards =
        [ card "a" "Bravo" "active" (Some "Hypothesis recorded, 2026-03-01 09:00:05 UTC")
          card "b" "alpha" "draft" (Some "Investigation started, 2026-03-01 09:00:09 UTC")
          card "c" "Charlie" "resolved" None ]
    let ids xs = xs |> List.map (fun (c: CardProjection) -> c.Id)
    Assert.Equal<string list>([ "b"; "a"; "c" ], UxView.sortCards "activity" cards |> ids)
    Assert.Equal<string list>([ "b"; "a"; "c" ], UxView.sortCards "title" cards |> ids)
    Assert.Equal<string list>([ "b"; "a"; "c" ], UxView.sortCards "status" cards |> ids)
    Assert.Equal<string list>([ "a"; "c"; "b" ], UxView.sortCards "title" [ cards[0]; cards[2]; { cards[1] with Title = "zulu" } ] |> ids)
    let items = UxView.cardItems "activity" "a" cards
    let sel = items |> List.map (fun i -> i["id"], i["selected"])
    Assert.Contains((ViewPrimitive.Text "a", ViewPrimitive.Text "true"), sel)
    Assert.Contains((ViewPrimitive.Text "b", ViewPrimitive.Text "false"), sel)

[<Fact>]
let ``VPO-885e4767b84371de VPO-02b53bd16db4ee17 an unknown count on a card is never shown as zero, and no next action is invented`` () =
    let items = UxView.cardItems "activity" "" [ card "a" "Bravo" "active" None ]
    let i = items.Head
    Assert.Equal(ViewPrimitive.Text "open questions: not known", i["questions"])
    Assert.Equal(ViewPrimitive.Text "evidence items: not known", i["evidence"])
    Assert.Equal(ViewPrimitive.Text "Latest activity: not recorded", i["activity"])
    Assert.Equal(ViewPrimitive.Flag false, i["hasNext"])
    Assert.Equal(ViewPrimitive.Text "", i["next"])

[<Fact>]
let ``VPO-141be05c7de03e17 when the engine's records are unavailable, next steps say so instead of showing no work`` () =
    let fixture = TestState.toProjection { Name = "x"; Predicates = []; Capabilities = []; Conflict = false }
    let ws = { fixture with Opened = fixture.Opened |> Map.map (fun _ l -> match l with Loaded i -> Loaded { i with Typed = None } | o -> o) }
    let v = View.project (model ws (Routes.Investigation(TestState.fixtureId, Routes.Overview)) "")
    Assert.True(flagOf v "next.unavailable")
    Assert.False(flagOf v "next.none")
    Assert.False(flagOf v "next.available")

/// The capabilities the engine allows for the investigation's current version, by the ids the shell renders.
let private engineAllowed (inv: InvestigationProjection) =
    let src = Option.get inv.Typed
    src.Projection.Capabilities
    |> List.choose (fun c -> EngineView.describe src.Projection.State c.Action |> Option.map (fun (id, _, _, _) -> id, c.Allowed))
    |> List.distinctBy fst
    |> List.sort

[<Fact>]
let ``VPO-be99a964783c59b5 the rendered commands are exactly the F# engine's capabilities for the current version, and move with it`` () =
    let entropy () = { Now = System.DateTime(2026, 10, 27, 12, 0, 0, System.DateTimeKind.Utc); Ids = List.init 4 (fun _ -> System.Guid.NewGuid()); Nonce = "n" }
    let port = EnginePort.create (EnginePort.Persistence.memory Map.empty [])
    let port, _ = port.Handle (entropy ()) (Command(None, "StartInvestigation", Map.ofList [ "title", "Valve leak"; "problemStatement", "Coolant found under valve 3." ], 0L))
    let id = match port.Projection.Investigations with Loaded [ c ] -> c.Id | o -> failwithf "%A" o
    let check (port: Port) (version: int64) =
        let inv = match port.Projection.Opened.TryFind id with Some(Loaded i) -> i | o -> failwithf "%A" o
        Assert.Equal(version, inv.Version)
        // The typed projection the capabilities came from is the current version, not an older one.
        Assert.Equal(version, (Option.get inv.Typed).Projection.Version)
        let rendered = inv.Capabilities |> List.map (fun c -> c.Id, c.Legal) |> List.distinctBy fst |> List.sort
        Assert.Equal<(string * bool) list>(engineAllowed inv, rendered)
        // And the view binds exactly those: every command button is one of them, legal only when the engine allows it.
        let view = View.project { Shell.initial with Port = port; Route = Routes.Investigation(id, Routes.Overview) }
        let allowed = Map.ofList (engineAllowed inv)
        match view.TryFind "capabilities" with
        | Some(ViewValue.Items items) ->
            for i in items do
                let cid = match i["id"] with ViewPrimitive.Text t -> t | _ -> ""
                let legal = (i.TryFind "legal" = Some(ViewPrimitive.Flag true)) || (i.TryFind "legalInput" = Some(ViewPrimitive.Flag true))
                Assert.True(allowed.ContainsKey cid, $"{cid} is not an engine capability")
                Assert.Equal(allowed[cid], legal)
        | o -> failwithf "%A" o
        for form in [ "AdmitEvidence"; "RelateEvidence"; "RecordCause"; "RecordCorrectiveAction" ] do
            Assert.Equal(view.TryFind("not." + form), Some(ViewValue.Flag(not (allowed.TryFind form |> Option.defaultValue false))))
        allowed
    let v1 = check port 1L
    Assert.True(v1["ActivateInvestigation"])
    let port, _ = port.Handle (entropy ()) (Command(Some id, "ActivateInvestigation", Map.empty, 1L))
    let v2 = check port 2L
    // Activation is no longer offered once the engine has applied it.
    Assert.False(v2["ActivateInvestigation"])
