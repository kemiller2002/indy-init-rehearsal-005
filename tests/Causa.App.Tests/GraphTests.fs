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

let private h id state = { Id = id; Statement = "Statement " + id; State = state; Supporting = Known 1; Contradicting = Unknown }

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
      Capabilities = [] }

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
    { Id = id; Statement = statement; State = state; Supporting = supporting; Contradicting = contradicting }

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
          [ hyp "h-1" "supported" (Known 3) (Known 0) "Root cause hypothesis"
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
let ``UI-005-O4 (links only; the click path is browser-tested) graph nodes link to their records by the application's own routes`` () =
    let p = inv [ h "a" "supported" ] [ { Id = "c1"; Outcome = "O1"; State = "identified"; Members = [ "a", RootCause ] } ]
    let root = parse (Graph.toDocument "T" p.Id (Graph.ofInvestigation p))
    let refs (id: string) =
        [ for n in root.GetProperty("nodes").EnumerateArray() do
              if n.GetProperty("id").GetString() = id then
                  for r in n.GetProperty("references").EnumerateArray() -> r.GetProperty("href").GetString() ]
    let hrefs = refs "h-a"
    Assert.Contains(Routes.formatItem (Routes.Investigation(p.Id, Routes.Hypotheses)) "a", hrefs)
    Assert.Contains(Routes.format (Routes.Investigation(p.Id, Routes.Evidence)), hrefs)
    Assert.Equal<string list>([ Routes.formatItem (Routes.Investigation(p.Id, Routes.CausalAnalysis)) "c1" ], refs "c-c1")
    // Every link resolves to a real route of the application (never NotFound).
    for href in hrefs @ refs "c-c1" do
        match Routes.parse href with
        | Routes.NotFound p -> failwith $"unroutable {p}"
        | _ -> ()
