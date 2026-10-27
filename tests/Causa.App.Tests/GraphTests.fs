/// T23: the typed domain-to-graph adapter. Test names carry the obligation ids they prove.
module Causa.App.Tests.GraphTests

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
      Proposals = []
      Capabilities = [] }

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
    for word in [ "\"x\":"; "\"y\":"; "position"; "width"; "height" ] do
        Assert.DoesNotContain(word, doc)
    Assert.Contains("\"format\":\"forma-workflow\"", doc)

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
