/// The typed domain-to-graph adapter (WDL-003): the causal relationships the engine projected become a Forma
/// workflow document. Responsibilities (WDL-004), each tested on its own:
///  - Causa (this module) owns the semantics: which nodes and edges exist, their words, their status text, their
///    tone (a function of the domain state only, WDL-008-O2) and the records they link to (UI-005-O4, UI-033).
///  - @echelon-foundry/forma-workflow owns placement and routing: the document carries no coordinates (WDL-002,
///    WDL-005) and asks only for the flow direction.
///  - Forma owns the visuals: tokens, components and focus treatment; Causa supplies only the UI-016 palette slots.
/// The output depends only on the projection and is ordered by stable ids (WDL-009). No causal claim is invented:
/// only hypotheses that are members of a recorded causal relationship, and the outcomes those relationships explain,
/// appear (WDL-003-O2). The graph is bipartite (hypothesis -> outcome), so it is acyclic by construction.
module Causa.App.Graph

open System.Text.Json
open System.Text.Json.Nodes
open Causa.App.Adapter

/// The UI-016 semantic palette (causa.css): the only colours the graph uses.
let palette =
    [ "navy", "#14283f"
      "teal", "#087f8c"
      "amber", "#e9a23b"
      "coral", "#d66b58"
      "ivory", "#f5f3ed" ]

/// The palette slot for a tone (Presentation.tone, the one tone mapping every view shares, UI-016-O13).
let slotOfTone (tone: string) =
    match tone with
    | "action" -> "teal"
    | "attention" -> "amber"
    | "conflict" -> "coral"
    | _ -> "navy"

type Reference = { Id: string; Kind: string; Label: string; Href: string }

type Node =
    { Id: string
      /// "hypothesis" or "outcome".
      Kind: string
      Label: string
      Description: string
      /// The domain state key (contract enum text) and its words: colour is never the only carrier (WDL-007-O2).
      State: string
      Status: string
      /// Palette slot, derived from `State` only (WDL-008-O2).
      Slot: string
      References: Reference list }

type Edge =
    { Id: string
      Source: string
      Target: string
      Label: string
      Role: CauseRole }

type CausalGraph = { Nodes: Node list; Edges: Edge list }

let hypothesisNode (id: string) = "h-" + id
let outcomeNode (id: string) = "c-" + id

/// Forma text fields hold at most 1000 characters; a longer statement is shortened on the node and kept whole in
/// the node's accessible description and in the text list (nothing is hidden, WDL-006-O1).
let maxLabel = 1000

let private shorten (s: string) = if s.Length <= maxLabel then s else s.Substring(0, maxLabel - 1) + "…"

/// Forma long text holds at most 20000 characters.
let private shortenLong (s: string) = if s.Length <= 20000 then s else s.Substring(0, 19999) + "…"

let private sectionHref (investigation: string) (section: Routes.Section) (item: string) =
    Routes.formatItem (Routes.Investigation(investigation, section)) item

/// Pure: the graph of an investigation projection.
let ofInvestigation (inv: InvestigationProjection) : CausalGraph =
    let members = inv.Causes |> List.collect (fun c -> c.Members |> List.map fst) |> Set.ofList

    let hypotheses =
        inv.Hypotheses
        |> List.filter (fun h -> members.Contains h.Id)
        |> List.sortBy (fun h -> h.Id)
        |> List.map (fun h ->
            { Id = hypothesisNode h.Id
              Kind = "hypothesis"
              Label = h.Statement
              Description = $"""{Presentation.count h.Supporting "supporting"}, {Presentation.count h.Contradicting "contradicting"} evidence"""
              State = h.State
              Status = Presentation.label h.State
              Slot = slotOfTone (Presentation.tone h.State)
              References =
                [ { Id = "hypothesis"; Kind = "hypothesis"; Label = "Hypothesis record"; Href = sectionHref inv.Id Routes.Hypotheses h.Id }
                  { Id = "evidence"; Kind = "evidence"; Label = "Evidence records"; Href = Routes.format (Routes.Investigation(inv.Id, Routes.Evidence)) } ] })

    let outcomes =
        inv.Causes
        |> List.sortBy (fun c -> c.Id)
        |> List.map (fun c ->
            { Id = outcomeNode c.Id
              Kind = "outcome"
              Label = c.Outcome
              Description = $"Causal relationship · {c.Members.Length} contributing hypotheses"
              State = c.State
              Status = Presentation.label c.State
              Slot = slotOfTone (Presentation.tone c.State)
              References = [ { Id = "cause"; Kind = "cause"; Label = "Causal relationship record"; Href = sectionHref inv.Id Routes.CausalAnalysis c.Id } ] })

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
                  Label = Presentation.roleLabel role
                  Role = role }))

    { Nodes = hypotheses @ outcomes; Edges = edges }

/// Line style and width per role: the role is also always written on the edge (never line or colour alone).
let lineOf (role: CauseRole) =
    match role with
    | RootCause -> "solid", 3
    | Trigger -> "solid", 2
    | ContributingFactor -> "dashed", 2
    | EnablingCondition -> "dotted", 2

/// "awaitingEvidence" -> "awaiting-evidence" (Forma namespaced states are lower-case).
let private kebab (s: string) =
    s
    |> Seq.collect (fun c -> if System.Char.IsUpper c then [ '-'; System.Char.ToLowerInvariant c ] else [ c ])
    |> Seq.toArray
    |> System.String

let private str (s: string) = JsonValue.Create s :> JsonNode
let private num (i: int) = JsonValue.Create i :> JsonNode
let private obj (members: (string * JsonNode) list) = JsonObject(dict members) :> JsonNode

let private arr (items: JsonNode seq) =
    let a = JsonArray()
    for i in items do
        a.Add i
    a :> JsonNode

let private legend () =
    [ "teal", "Supported or confirmed", None
      "amber", "Open: proposed, plausible or awaiting work", None
      "coral", "Weakened or falsified", None
      "navy", "Other recorded states", None ]
    |> List.map (fun (slot, label, _) -> obj [ "id", str ("tone-" + slot); "label", str label; "color", obj [ "stroke", str ("palette:" + slot) ] ])
    |> fun tones ->
        tones
        @ ([ RootCause; Trigger; ContributingFactor; EnablingCondition ]
           |> List.map (fun role ->
               let line, _ = lineOf role
               let key =
                   match role with
                   | RootCause -> "root-cause"
                   | Trigger -> "trigger"
                   | ContributingFactor -> "contributing-factor"
                   | EnablingCondition -> "enabling-condition"
               obj [ "id", str ("role-" + key); "label", str (Presentation.roleLabel role); "line", str line ]))

/// The Forma portable workflow document (format 1.0.0) for a graph; empty when there is nothing to draw.
let toDocument (title: string) (investigationId: string) (g: CausalGraph) : string =
    if g.Nodes.IsEmpty then
        ""
    else
        let node (n: Node) =
            let kindLabel = if n.Kind = "hypothesis" then "Hypothesis" else "Outcome"
            let members =
                [ "id", str n.Id
                  "kind", str ("org.causa.graph:" + n.Kind)
                  "kindLabel", str kindLabel
                  "label", str (shorten n.Label)
                  "description", str (shortenLong n.Description)
                  "variant", str (if n.Kind = "hypothesis" then "rounded" else "pill")
                  "color", obj [ "fill", str "palette:ivory"; "stroke", str ("palette:" + n.Slot); "accent", str ("palette:" + n.Slot) ]
                  "status", obj [ "state", str ("org.causa.state:" + kebab n.State); "label", str n.Status ]
                  "accessibility", obj [ "name", str (shorten $"{kindLabel} ({n.Status}): {n.Label}"); "description", str (shortenLong (n.Label + " · " + n.Description)) ]
                  "references",
                  arr (
                      n.References
                      |> List.map (fun r -> obj [ "id", str r.Id; "system", str "org.causa"; "type", str r.Kind; "key", str r.Href; "label", str r.Label; "href", str r.Href ])
                  )
                  "interaction", obj [ "action", str "navigate"; "target", obj [ "reference", str (List.head n.References).Id ]; "label", str ("Open " + (List.head n.References).Label.ToLowerInvariant()) ] ]
            obj members

        let edge (e: Edge) =
            let line, width = lineOf e.Role
            obj
                [ "id", str e.Id
                  "source", obj [ "node", str e.Source ]
                  "target", obj [ "node", str e.Target ]
                  "label", str e.Label
                  "line", str line
                  "width", num width
                  "color", obj [ "stroke", str "palette:navy" ] ]

        let doc =
            obj
                [ "format", str "forma-workflow"
                  "formatVersion", str "1.0.0"
                  "id", str ("causal-" + investigationId)
                  "title", str (shorten ("Causal analysis: " + title))
                  "language", str "en"
                  "forma", obj [ "version", str "0.9.0" ]
                  "palette", obj (palette |> List.map (fun (slot, hex) -> slot, str hex))
                  "layout", obj [ "direction", str "down" ]
                  "presentation", obj [ "legendTitle", str "Key"; "legend", arr (legend ()) ]
                  "nodes", arr (g.Nodes |> List.map node)
                  "edges", arr (g.Edges |> List.map edge) ]

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
