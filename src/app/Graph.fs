/// The typed domain-to-graph adapter (WDL-003): the causal relationships the engine projected become a Forma
/// workflow document. Causa owns the semantics (which nodes, which edges, which words); the Forma workflow engine
/// owns placement and routing (WDL-004), so there are no coordinates here (WDL-002, WDL-005). The output depends
/// only on the projection and is ordered by stable ids (WDL-009). No causal claim is invented: only hypotheses that
/// are members of a recorded causal relationship and the outcomes those relationships explain appear (WDL-003-O2).
module Causa.App.Graph

open System.Text.Json
open System.Text.Json.Nodes
open Causa.App.Adapter

type Node = { Id: string; Kind: string; Label: string; Description: string }
type Edge = { Id: string; Source: string; Target: string; Label: string }
type CausalGraph = { Nodes: Node list; Edges: Edge list }

let hypothesisNode (id: string) = "h-" + id
let outcomeNode (id: string) = "c-" + id

/// Pure: the graph of an investigation projection.
let ofInvestigation (inv: InvestigationProjection) : CausalGraph =
    let members = inv.Causes |> List.collect (fun c -> c.Members |> List.map fst) |> Set.ofList

    let hypotheses =
        inv.Hypotheses
        |> List.filter (fun h -> members.Contains h.Id)
        |> List.sortBy (fun h -> h.Id)
        |> List.map (fun h ->
            { Id = hypothesisNode h.Id
              Kind = "task"
              Label = h.Statement
              Description =
                $"""Hypothesis · {Presentation.label h.State} · {Presentation.count h.Supporting "supporting"}, {Presentation.count h.Contradicting "contradicting"} evidence""" })

    let outcomes =
        inv.Causes
        |> List.sortBy (fun c -> c.Id)
        |> List.map (fun c ->
            { Id = outcomeNode c.Id
              Kind = "end"
              Label = c.Outcome
              Description = $"Outcome · causal relationship {Presentation.label c.State}" })

    let known = hypotheses |> List.map (fun n -> n.Id) |> Set.ofList

    let edges =
        inv.Causes
        |> List.sortBy (fun c -> c.Id)
        |> List.collect (fun c ->
            c.Members
            |> List.sortBy fst
            |> List.filter (fun (h, _) -> known.Contains(hypothesisNode h))
            |> List.map (fun (h, role) ->
                { Id = "e-" + c.Id + "-" + h
                  Source = hypothesisNode h
                  Target = outcomeNode c.Id
                  Label = Presentation.roleLabel role }))

    { Nodes = hypotheses @ outcomes; Edges = edges }

/// The Forma portable workflow document (format 1.0.0) for a graph; empty when there is nothing to draw.
let toDocument (title: string) (investigationId: string) (g: CausalGraph) : string =
    if g.Nodes.IsEmpty then ""
    else
        let str (s: string) = JsonValue.Create s :> JsonNode

        let nodes = JsonArray()

        for n in g.Nodes do
            nodes.Add(JsonObject(dict [ "id", str n.Id; "kind", str n.Kind; "label", str n.Label; "description", str n.Description ]))

        let edges = JsonArray()

        for e in g.Edges do
            edges.Add(
                JsonObject(
                    dict
                        [ "id", str e.Id
                          "source", (JsonObject(dict [ "node", str e.Source ]) :> JsonNode)
                          "target", (JsonObject(dict [ "node", str e.Target ]) :> JsonNode)
                          "label", str e.Label ]
                )
            )

        let doc =
            JsonObject(
                dict
                    [ "format", str "forma-workflow"
                      "formatVersion", str "1.0.0"
                      "id", str ("causal-" + investigationId)
                      "title", str ("Causal analysis: " + title)
                      "forma", (JsonObject(dict [ "version", str "0.9.0" ]) :> JsonNode)
                      "nodes", (nodes :> JsonNode)
                      "edges", (edges :> JsonNode) ]
            )

        doc.ToJsonString(JsonSerializerOptions(WriteIndented = false))

/// What a selected node stands for, from the authoritative records only (UI-026-O3); empty for an unknown id.
let describeSelection (inv: InvestigationProjection) (selected: string) : string =
    if selected.StartsWith "h-" then
        let id = selected.Substring 2

        match inv.Hypotheses |> List.tryFind (fun h -> h.Id = id) with
        | Some h ->
            $"""Hypothesis ({Presentation.label h.State}): {h.Statement} · {Presentation.count h.Supporting "supporting"}, {Presentation.count h.Contradicting "contradicting"} evidence"""
        | None -> ""
    elif selected.StartsWith "c-" then
        let id = selected.Substring 2

        match inv.Causes |> List.tryFind (fun c -> c.Id = id) with
        | Some c -> $"Outcome: {c.Outcome} · {Presentation.label c.State} · {c.Members.Length} contributing hypotheses"
        | None -> ""
    else
        ""
