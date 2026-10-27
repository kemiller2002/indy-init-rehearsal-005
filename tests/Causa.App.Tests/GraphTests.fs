/// The causal graph, Causa's layer (WDL-003, WDL-004, WDL-008, WDL-011): the typed domain-to-graph projection and
/// the deterministic synthetic graph fixtures the browser layout tests use. Test names carry the obligation ids they
/// prove. The fixtures are synthetic (no historical or R001 record); they are written to
/// tests/browser/graph-fixtures/ and the committed files must equal what this projection produces
/// (regenerate with CAUSA_WRITE_GRAPH_FIXTURES=1).
module Causa.App.Tests.GraphTests

open System
open System.IO
open System.Text
open System.Text.Json
open Xunit
open Causa.App
open Causa.App.Adapter

let private h id state = { Id = id; Statement = "Statement " + id; State = state; Supporting = Known 1; Contradicting = Unknown; Evidence = [] }

let private inv hypotheses causes =
    { Id = "00000000-0000-4000-8000-0000000000a1"
      Title = "T"
      Version = 3L
      Lifecycle = "active"
      Writable = false
      Description = None
      Hypotheses = hypotheses
      Evidence = []
      Blockers = []
      Causes = causes
      Timeline = []
      Proposals = []
      Findings = None
      Integrity = None
      Report = None
      Capabilities = []
      Typed = None }

let private parse (doc: string) = JsonDocument.Parse(doc).RootElement

[<Fact>]
let ``VPO-0a171a55852aaf80 VPO-d2d5200b61947ce8 only recorded causal members and their outcomes become nodes, each role labelled`` () =
    let g =
        Graph.ofInvestigation (
            inv
                [ h "a" "supported"; h "b" "proposed"; h "unrelated" "proposed" ]
                [ { Id = "c1"; Outcome = "O1"; State = "identified"; Members = [ "a", RootCause; "b", Trigger ] }
                  { Id = "c2"; Outcome = "O2"; State = "confirmed"; Members = [ "b", ContributingFactor; "ghost", EnablingCondition ] } ]
        )

    Assert.Equal<string list>([ "h-a"; "h-b"; "c-c1"; "c-c2" ], g.Nodes |> List.map (fun n -> n.Id))
    Assert.Equal<string list>([ "Root cause"; "Trigger"; "Contributing factor" ], g.Edges |> List.map (fun e -> e.Label))
    // A member the projection does not carry is not drawn as an invented node.
    Assert.DoesNotContain(g.Edges, fun e -> e.Source = "h-ghost")

[<Fact>]
let ``VPO-434d1a1d24b4b20e VPO-69f661459a587bbd the document carries no coordinates: placement is left to the workflow engine`` () =
    let doc = Graph.toDocument "T" "x" (Graph.ofInvestigation (inv [ h "a" "proposed" ] [ { Id = "c1"; Outcome = "O"; State = "identified"; Members = [ "a", RootCause ] } ]))
    let root = parse doc
    Assert.Equal("forma-workflow", root.GetProperty("format").GetString())
    for collection in [ "nodes"; "edges" ] do
        for item in root.GetProperty(collection).EnumerateArray() do
            Assert.False(item.TryGetProperty("layout") |> fst, $"{collection} item carries layout")
    // The only layout request is the flow direction; no canvas, coordinates or waypoints.
    let layout = root.GetProperty("layout")
    Assert.Equal<string list>([ "direction" ], [ for p in layout.EnumerateObject() -> p.Name ])
    for word in [ "\"x\":"; "\"y\":"; "position"; "waypoints"; "canvas" ] do
        Assert.DoesNotContain(word, doc)

[<Fact>]
let ``VPO-2a3f7cbe7df14c03 zero, one, many and disconnected graphs are all valid`` () =
    Assert.Equal("", Graph.toDocument "T" "x" (Graph.ofInvestigation (inv [] [])))
    let one = Graph.ofInvestigation (inv [] [ { Id = "c1"; Outcome = "O"; State = "identified"; Members = [] } ])
    Assert.Single(one.Nodes) |> ignore
    let many =
        Graph.ofInvestigation (
            inv
                [ for i in 1..30 -> h (sprintf "h%02d" i) "proposed" ]
                [ for i in 1..10 -> { Id = sprintf "c%02d" i; Outcome = "O"; State = "identified"; Members = [ sprintf "h%02d" i, RootCause; sprintf "h%02d" (i + 10), Trigger ] } ]
        )
    Assert.Equal(30, many.Nodes.Length)
    Assert.Equal(20, many.Edges.Length)

[<Fact>]
let ``VPO-531b33816ba9cddf VPO-7f9018a0a0d1e32b determinism: the same projection in any order gives the same document`` () =
    let causes = [ { Id = "c2"; Outcome = "O2"; State = "identified"; Members = [ "b", Trigger; "a", RootCause ] }; { Id = "c1"; Outcome = "O1"; State = "identified"; Members = [ "a", RootCause ] } ]
    let a = Graph.toDocument "T" "x" (Graph.ofInvestigation (inv [ h "a" "proposed"; h "b" "proposed" ] causes))
    let b = Graph.toDocument "T" "x" (Graph.ofInvestigation (inv [ h "b" "proposed"; h "a" "proposed" ] (List.rev causes)))
    Assert.Equal(a, b)

[<Fact>]
let ``VPO-b36c63fba6f83d4c VPO-1c3905d9fa746df0 a selected node reveals its authoritative record and evidence counts`` () =
    let i = inv [ h "a" "supported" ] [ { Id = "c1"; Outcome = "O1"; State = "identified"; Members = [ "a", RootCause ] } ]
    Assert.Equal("Hypothesis (Supported): Statement a · 1 supporting, contradicting: not known evidence", Graph.describeSelection i "h-a")
    Assert.Equal("Outcome: O1 · Open · 1 contributing hypotheses", Graph.describeSelection i "c-c1")
    Assert.Equal("", Graph.describeSelection i "h-missing")

// ---- synthetic graph fixtures (WDL-011-O1) ----

let private fixtureInvestigation = "00000000-0000-4000-8000-00000000a0a0"

let private hyp id state supporting contradicting statement =
    { Id = id; Statement = statement; State = state; Supporting = supporting; Contradicting = contradicting; Evidence = [] }

let private projection title hypotheses causes = { inv hypotheses causes with Id = fixtureInvestigation; Title = title }

let private longText n = String.Join(" ", Seq.init n (fun i -> $"segment{i:D3} of a deliberately long synthetic hypothesis statement"))

/// name, purpose, projection (synthetic; ids and words are invented for the test).
let fixtures: (string * string * InvestigationProjection) list =
    [ "empty", "No causal relationship recorded: nothing to draw.", projection "Empty" [ hyp "h-1" "proposed" (Known 0) (Known 0) "Unlinked hypothesis" ] []
      "single-node", "One outcome without members.", projection "Single node" [] [ { Id = "c-1"; Outcome = "Single outcome with no recorded members"; State = "proposed"; Members = [] } ]
      "four-roles",
      "One outcome explained by four hypotheses, one per cause role.",
      projection
          "Four roles"
          [ { hyp "h-1" "supported" (Known 3) (Known 0) "Root cause hypothesis" with Evidence = [ "e-1"; "e-2"; "e-3" ] }
            hyp "h-2" "plausible" (Known 1) (Known 1) "Trigger hypothesis"
            hyp "h-3" "weakened" (Known 1) (Known 2) "Contributing factor hypothesis"
            hyp "h-4" "proposed" Unknown Unknown "Enabling condition hypothesis" ]
          [ { Id = "c-1"; Outcome = "The synthetic outcome"; State = "confirmed"; Members = [ "h-1", RootCause; "h-2", Trigger; "h-3", ContributingFactor; "h-4", EnablingCondition ] } ]
      "dense-evidence",
      "Twelve hypotheses with large evidence counts across three outcomes, shared members included.",
      projection
          "Dense evidence"
          [ for i in 1..12 -> hyp $"h-{i:D2}" (if i % 3 = 0 then "supported" elif i % 3 = 1 then "plausible" else "falsified") (Known(37 * i)) (Known(i * i)) $"Dense hypothesis {i:D2}" ]
          [ for c in 1..3 ->
                { Id = $"c-{c}"
                  Outcome = $"Dense outcome {c}"
                  State = (if c = 1 then "confirmed" else "proposed")
                  Members =
                    [ for i in 1..12 do
                          if (i - 1) % 3 = c - 1 || i = c then
                              yield
                                  $"h-{i:D2}",
                                  (match i % 4 with
                                   | 0 -> RootCause
                                   | 1 -> Trigger
                                   | 2 -> ContributingFactor
                                   | _ -> EnablingCondition) ] } ]
      "long-labels",
      "Very long hypothesis and outcome text, one beyond the 1000-character node label limit.",
      projection
          "Long labels"
          [ hyp "h-1" "supported" (Known 2) (Known 0) (longText 8)
            hyp "h-2" "plausible" (Known 0) (Known 1) (longText 30) ]
          [ { Id = "c-1"; Outcome = longText 6; State = "proposed"; Members = [ "h-1", RootCause; "h-2", ContributingFactor ] } ]
      "disconnected",
      "Two causal relationships that share no hypothesis.",
      projection
          "Disconnected"
          [ hyp "h-1" "supported" (Known 2) (Known 0) "Left cluster hypothesis A"
            hyp "h-2" "proposed" (Known 1) (Known 0) "Left cluster hypothesis B"
            hyp "h-3" "falsified" (Known 0) (Known 3) "Right cluster hypothesis" ]
          [ { Id = "c-1"; Outcome = "Left outcome"; State = "proposed"; Members = [ "h-1", RootCause; "h-2", Trigger ] }
            { Id = "c-2"; Outcome = "Right outcome"; State = "reopened"; Members = [ "h-3", RootCause ] } ] ]

/// A layout-only fixture: Causa's graph is bipartite (hypothesis -> outcome) and cannot contain a cycle, so the
/// "permitted cycles" case is authored directly as a workflow document to test the layout engine on its own.
let cycleDocument =
    """{"format":"forma-workflow","formatVersion":"1.0.0","id":"layout-cycle","title":"Layout only: a permitted cycle","language":"en","forma":{"version":"0.9.0"},"layout":{"direction":"down"},"nodes":[{"id":"a","kind":"task","label":"Step A"},{"id":"b","kind":"task","label":"Step B"},{"id":"c","kind":"task","label":"Step C"}],"edges":[{"id":"ab","source":{"node":"a"},"target":{"node":"b"},"label":"a to b"},{"id":"bc","source":{"node":"b"},"target":{"node":"c"},"label":"b to c"},{"id":"ca","source":{"node":"c"},"target":{"node":"a"},"label":"c back to a"}]}"""

let private repoRoot =
    lazy
        (let rec up (dir: DirectoryInfo) =
            if isNull dir then failwith "repository root (App.slnx) not found"
            elif File.Exists(Path.Combine(dir.FullName, "App.slnx")) then dir.FullName
            else up dir.Parent
         up (DirectoryInfo AppContext.BaseDirectory))

let private renderFixture (name: string) (purpose: string) (document: string) =
    let o = Nodes.JsonObject()
    o["name"] <- Nodes.JsonValue.Create name
    o["purpose"] <- Nodes.JsonValue.Create purpose
    o["synthetic"] <- Nodes.JsonValue.Create true
    o["document"] <- (if document = "" then null else Nodes.JsonNode.Parse document)
    o.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n"

let renderedFixtures () =
    [ for name, purpose, p in fixtures do
          yield name, renderFixture name purpose (Graph.toDocument p.Title p.Id (Graph.ofInvestigation p))
      yield "cycle", renderFixture "cycle" "Layout only: a permitted cycle (Causa's own graph is acyclic by construction)." cycleDocument ]

[<Fact>]
[<Trait("obligation", "VPO-0fa99d435ca39516")>]
let ``WDL-011-O1 VPO-0fa99d435ca39516 the deterministic synthetic graph fixtures are committed exactly as the projection produces them`` () =
    let dir = Path.Combine(repoRoot.Value, "tests", "browser", "graph-fixtures")
    let rendered = renderedFixtures ()
    if Environment.GetEnvironmentVariable "CAUSA_WRITE_GRAPH_FIXTURES" = "1" then
        Directory.CreateDirectory dir |> ignore
        for name, text in rendered do
            File.WriteAllText(Path.Combine(dir, name + ".json"), text, UTF8Encoding false)
    Assert.Equal<string list>(
        [ "cycle"; "dense-evidence"; "disconnected"; "empty"; "four-roles"; "long-labels"; "single-node" ],
        rendered |> List.map fst |> List.sort
    )
    for name, text in rendered do
        let path = Path.Combine(dir, name + ".json")
        Assert.True(File.Exists path, $"{path} missing: run with CAUSA_WRITE_GRAPH_FIXTURES=1")
        Assert.Equal(text, File.ReadAllText(path, UTF8Encoding false))
    // Rendering twice gives the same bytes (deterministic).
    Assert.Equal<(string * string) list>(rendered, renderedFixtures ())
    // The fixture shapes: empty draws nothing; four roles has one edge per role; the dense one carries large counts.
    let docOf name = rendered |> List.find (fun (n, _) -> n = name) |> snd |> parse |> fun r -> r.GetProperty("document")
    Assert.Equal(JsonValueKind.Null, (docOf "empty").ValueKind)
    Assert.Equal(1, (docOf "single-node").GetProperty("nodes").GetArrayLength())
    let roles = [ for e in (docOf "four-roles").GetProperty("edges").EnumerateArray() -> e.GetProperty("label").GetString() ] |> List.sort
    Assert.Equal<string list>([ "Contributing factor"; "Enabling condition"; "Root cause"; "Trigger" ], roles)
    Assert.Contains("444 supporting", (docOf "dense-evidence").GetRawText())

[<Fact>]
[<Trait("obligation", "VPO-beeaf5b2a56c521d")>]
[<Trait("obligation", "VPO-17ea30ea2f160b62")>]
let ``WDL-004-O1 WDL-008-O2 VPO-beeaf5b2a56c521d VPO-17ea30ea2f160b62 Causa supplies semantics and domain-state tones only; geometry and visuals are left to the engine and Forma`` () =
    let state s = hyp "h" s (Known 1) (Known 0) "S"
    let docFor s = Graph.toDocument "T" "x" (Graph.ofInvestigation (inv [ state s ] [ { Id = "c"; Outcome = "O"; State = "proposed"; Members = [ "h", RootCause ] } ]))
    let strokeOf (doc: string) =
        let n = (parse doc).GetProperty("nodes")[0]
        n.GetProperty("color").GetProperty("stroke").GetString()
    // The tone is a function of the domain state alone ...
    for s, slot in [ "supported", "teal"; "plausible", "amber"; "proposed", "amber"; "weakened", "coral"; "falsified", "coral"; "superseded", "navy" ] do
        Assert.Equal("palette:" + slot, strokeOf (docFor s))
    // ... not of the words, ids or counts: the same state with other text gets the same colour.
    let other = Graph.toDocument "Other" "y" (Graph.ofInvestigation (inv [ { (state "supported") with Id = "z"; Statement = "Different words"; Supporting = Known 99 } ] [ { Id = "q"; Outcome = "Q"; State = "proposed"; Members = [ "z", Trigger ] } ]))
    Assert.Equal(strokeOf (docFor "supported"), strokeOf other)
    // The palette is exactly the UI-016 colours, as Forma palette slots (Forma resolves them; Causa draws nothing).
    let paletteJson = (parse (docFor "supported")).GetProperty("palette")
    Assert.Equal<(string * string) list>(
        [ "amber", "#e9a23b"; "coral", "#d66b58"; "ivory", "#f5f3ed"; "navy", "#14283f"; "teal", "#087f8c" ],
        [ for p in paletteJson.EnumerateObject() -> p.Name, p.Value.GetString() ] |> List.sort
    )
    // Every coloured node and every edge also carries its meaning as text (WDL-007-O2).
    let root = parse (docFor "weakened")
    for n in root.GetProperty("nodes").EnumerateArray() do
        Assert.False(String.IsNullOrWhiteSpace(n.GetProperty("status").GetProperty("label").GetString()))
    for e in root.GetProperty("edges").EnumerateArray() do
        Assert.False(String.IsNullOrWhiteSpace(e.GetProperty("label").GetString()))

[<Fact>]
[<Trait("obligation", "VPO-c7c84a08e62427e4")>]
let ``UI-005-O4 VPO-c7c84a08e62427e4 a hypothesis node opens its own evidence and every link is an application route`` () =
    let withEvidence = { h "a" "supported" with Evidence = [ "e1"; "e2" ] }
    let p = inv [ withEvidence; h "b" "proposed" ] [ { Id = "c1"; Outcome = "O1"; State = "identified"; Members = [ "a", RootCause; "b", Trigger ] } ]
    let root = parse (Graph.toDocument "T" p.Id (Graph.ofInvestigation p))
    let node (id: string) = root.GetProperty("nodes").EnumerateArray() |> Seq.find (fun n -> n.GetProperty("id").GetString() = id)
    let target (n: JsonElement) =
        let refId = n.GetProperty("interaction").GetProperty("target").GetProperty("reference").GetString()
        n.GetProperty("references").EnumerateArray() |> Seq.find (fun r -> r.GetProperty("id").GetString() = refId)
    // The node's Open intent goes to its first related evidence record in the evidence view.
    Assert.Equal(Routes.formatItem (Routes.Investigation(p.Id, Routes.Evidence)) "e1", (target (node "h-a")).GetProperty("href").GetString())
    Assert.Equal("Evidence (2 related)", (target (node "h-a")).GetProperty("label").GetString())
    // Without related evidence it opens the hypothesis record; an outcome opens its causal relationship.
    Assert.Equal(Routes.formatItem (Routes.Investigation(p.Id, Routes.Hypotheses)) "b", (target (node "h-b")).GetProperty("href").GetString())
    Assert.Equal(Routes.formatItem (Routes.Investigation(p.Id, Routes.CausalAnalysis)) "c1", (target (node "c-c1")).GetProperty("href").GetString())
    // Every link resolves to a real route of the application (never NotFound) and satisfies the kernel's allow-list.
    for n in root.GetProperty("nodes").EnumerateArray() do
        for r in n.GetProperty("references").EnumerateArray() do
            let href = r.GetProperty("href").GetString()
            Assert.StartsWith("#/investigations/", href)
            match Routes.parse href with
            | Routes.NotFound path -> failwith $"unroutable {path}"
            | _ -> ()

[<Fact>]
[<Trait("obligation", "VPO-c7c84a08e62427e4")>]
let ``UI-005-O4 VPO-c7c84a08e62427e4 the shell's hypothesis rows carry the evidence the engine relates to them, in admission order`` () =
    let ns = Guid "6f1e2d3c-4b5a-4968-8778-695a4b3c2d1e"
    let gid (n: string) = IndyInitRootCauseInvestigator.Engine.Hashing.uuidV5 ns n
    let id n : IndyInitRootCauseInvestigator.Engine.Id<'T> = IndyInitRootCauseInvestigator.Engine.Id(gid n)
    let human: IndyInitRootCauseInvestigator.Engine.Actor = { ActorId = "investigator-01"; Kind = IndyInitRootCauseInvestigator.Engine.Human }
    let t0 = DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero)
    let invId: IndyInitRootCauseInvestigator.Engine.InvestigationId = id "inv"
    let commands =
        let open' = IndyInitRootCauseInvestigator.Engine.Command.StartInvestigation("Synthetic", id "obs", "Synthetic observation.")
        [ open'
          IndyInitRootCauseInvestigator.Engine.ActivateInvestigation
          IndyInitRootCauseInvestigator.Engine.RecordHypothesis(id "h", "Synthetic hypothesis", None)
          IndyInitRootCauseInvestigator.Engine.AdmitEvidence(id "e-first", IndyInitRootCauseInvestigator.Engine.TextInput "First", IndyInitRootCauseInvestigator.Engine.Log, "log", None)
          IndyInitRootCauseInvestigator.Engine.AdmitEvidence(id "e-second", IndyInitRootCauseInvestigator.Engine.TextInput "Second", IndyInitRootCauseInvestigator.Engine.Log, "log", None)
          IndyInitRootCauseInvestigator.Engine.AdmitEvidence(id "e-unrelated", IndyInitRootCauseInvestigator.Engine.TextInput "Other", IndyInitRootCauseInvestigator.Engine.Log, "log", None)
          // related in the opposite order to admission, once supporting and once contradicting
          IndyInitRootCauseInvestigator.Engine.RelateEvidence(id "r1", id "e-second", id "h", IndyInitRootCauseInvestigator.Engine.Contradicts, None)
          IndyInitRootCauseInvestigator.Engine.RelateEvidence(id "r2", id "e-first", id "h", IndyInitRootCauseInvestigator.Engine.Supports, None)
          IndyInitRootCauseInvestigator.Engine.RelateEvidence(id "r3", id "e-first", id "h", IndyInitRootCauseInvestigator.Engine.Discriminates, None) ]
    let session =
        commands
        |> List.indexed
        |> List.fold
            (fun s (i, c) ->
                match
                    IndyInitRootCauseInvestigator.Engine.Kernel.submit
                        s
                        ({ CommandId = gid $"c{i}"; Investigation = invId; ExpectedVersion = None; Actor = human; RequestedAt = t0.AddMinutes(float i); Command = c }: IndyInitRootCauseInvestigator.Engine.CommandEnvelope)
                with
                | Ok a -> a.Session
                | Error r -> failwithf "%A" r)
            IndyInitRootCauseInvestigator.Engine.Kernel.empty
    let state = session.State.Value
    let projection = IndyInitRootCauseInvestigator.Engine.Projection.ofInvestigation human t0 None state
    let view = EngineView.ofProjection true session.History projection
    let row = view.Hypotheses |> List.exactlyOne
    Assert.Equal<string list>([ (gid "e-first").ToString("D"); (gid "e-second").ToString("D") ], row.Evidence)

// ---- the graph of the real historical investigations, offline (read from a causa-data checkout at run time) ----

let private causaData () =
    match Environment.GetEnvironmentVariable "CAUSA_DATA_DIR" with
    | null
    | "" ->
        let rec up (d: DirectoryInfo) =
            if isNull d then failwith "causa-data checkout not found (CAUSA_DATA_DIR)"
            elif Directory.Exists(Path.Combine(d.FullName, "causa-data", "catalog")) then Path.Combine(d.FullName, "causa-data")
            else up d.Parent
        up (DirectoryInfo AppContext.BaseDirectory)
    | dir -> dir

[<Fact>]
let ``WDL-012 (offline layer) the graph of each historical case is exactly its recorded causal structure with its evidence counts`` () =
    let data = causaData ()
    let active = JsonDocument.Parse(File.ReadAllText(Path.Combine(data, "catalog", "active.json"))).RootElement
    let manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(data, active.GetProperty("manifest").GetString()))).RootElement
    let human: IndyInitRootCauseInvestigator.Engine.Actor = { ActorId = "viewer"; Kind = IndyInitRootCauseInvestigator.Engine.Human }
    let cases = [ for c in manifest.GetProperty("cases").EnumerateArray() -> c ]
    Assert.Equal(3, cases.Length)
    for c in cases do
        let id = c.GetProperty("investigationId").GetString()
        let dir = Path.Combine(data, c.GetProperty("path").GetString())
        let files = Directory.GetFiles dir |> Array.map (fun f -> Path.GetFileName f, File.ReadAllBytes f) |> Map.ofArray
        let loaded =
            match IndyInitRootCauseInvestigator.Engine.Storage.read id files with
            | Ok l -> l
            | Error e -> failwithf "%A" e
        // Independently from the events: causes, members with roles, and supports/contradicts per hypothesis.
        let events = File.ReadAllLines(Path.Combine(dir, "events.ndjson")) |> Array.filter (fun l -> l <> "") |> Array.map (fun l -> JsonDocument.Parse(l).RootElement)
        let roleLabel = Map.ofList [ "rootCause", "Root cause"; "trigger", "Trigger"; "contributingFactor", "Contributing factor"; "enablingCondition", "Enabling condition" ]
        let causes =
            [ for e in events do
                  if e.GetProperty("eventType").GetString() = "CausalAssertionRecorded" then
                      let p = e.GetProperty("payload")
                      yield p.GetProperty("causeId").GetString(), [ for m in p.GetProperty("members").EnumerateArray() -> m.GetProperty("hypothesisId").GetString(), m.GetProperty("role").GetString() ] ]
        let members = causes |> List.collect (snd >> List.map fst) |> Set.ofList
        let related kind h =
            events
            |> Array.filter (fun e -> e.GetProperty("eventType").GetString() = "EvidenceRelated")
            |> Array.filter (fun e -> let p = e.GetProperty("payload") in p.GetProperty("hypothesisId").GetString() = h && p.GetProperty("kind").GetString() = kind)
            |> Array.length
        let view = EngineView.ofProjection false loaded.Events (IndyInitRootCauseInvestigator.Engine.Projection.ofInvestigation human DateTimeOffset.UnixEpoch None loaded.State)
        let g = Graph.ofInvestigation view
        Assert.Equal<string list>(
            ([ for h in members -> "h-" + h ] @ [ for cid, _ in causes -> "c-" + cid ]) |> List.sort,
            g.Nodes |> List.map (fun n -> n.Id) |> List.sort
        )
        Assert.Equal<string list>(
            [ for cid, ms in causes do for h, r in ms -> $"{h}->{cid}:{roleLabel[r]}" ] |> List.sort,
            [ for e in g.Edges -> $"{e.Source.Substring 2}->{e.Target.Substring 2}:{e.Label}" ] |> List.sort
        )
        for n in g.Nodes |> List.filter (fun n -> n.Kind = "hypothesis") do
            let h = n.Id.Substring 2
            let supports = related "supports" h
            let contradicts = related "contradicts" h
            Assert.Equal($"{supports} supporting, {contradicts} contradicting evidence", n.Description)
        // And the document for it is one the layout engine accepts by its own limits (labels within 1000 chars).
        let documentText = Graph.toDocument view.Title view.Id g
        // Diagnostics only: write the documents somewhere outside the repository for an offline layout check.
        match Environment.GetEnvironmentVariable "CAUSA_GRAPH_DUMP_DIR" with
        | null
        | "" -> ()
        | out -> File.WriteAllText(Path.Combine(out, id + ".json"), documentText)
        let doc = JsonDocument.Parse(documentText).RootElement
        for n in doc.GetProperty("nodes").EnumerateArray() do
            Assert.True(n.GetProperty("label").GetString().Length <= 1000)
            Assert.True(n.GetProperty("accessibility").GetProperty("name").GetString().Length <= 1000)
