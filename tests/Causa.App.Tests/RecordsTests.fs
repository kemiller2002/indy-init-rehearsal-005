/// UI-005, UI-032..UI-040: relationships, the inspector, explanations and next steps from the engine's typed records.
/// The investigation is synthetic and built through Kernel.submit (SyntheticCase.fs).
module Causa.App.Tests.RecordsTests

open System
open Xunit
open IndyInitRootCauseInvestigator.Engine
open Causa.App
open Causa.App.Records
open Causa.App.Tests.SyntheticCase

let private src = lazy (source (run full))
let private refOf kind (id: Id<'T>) = { Kind = kind; Id = id.Text }
let private relation (i: Inspected) name = i.Relations |> List.find (fun r -> r.Name = name)
let private inspected r = inspect src.Value r |> Option.get
let private value (i: Inspected) label = i.Fields |> List.find (fun f -> f.Label = label)

[<Fact>]
let ``VPO-ff2e466b7aaa3e12 the evidence inspector shows source, timestamps, original content, assessment and links to hypotheses`` () =
    let i = inspected (refOf EvidenceRecord eTemp)
    Assert.Equal("Casing temperature 96 °C at 02:10.", (value i "Original content").Value)
    Assert.Equal("Control-room trend log", (value i "Source").Value)
    Assert.Equal("Measurement", (value i "Category").Value)
    Assert.Equal("2026-03-01 02:00:00 UTC", (value i "Captured at").Value)
    Assert.Equal("investigator-a", (value i "Recorded by").Value)
    Assert.False((value i "Recorded at").Missing)
    Assert.Equal("Not yet assessed", (value i "Assessment").Value)
    // Explicit links to the hypotheses it supports and contradicts, by stable id.
    Assert.Empty((relation i "Hypotheses it supports").Links)
    let con = (relation i "Hypotheses it contradicts").Links
    Assert.Equal(1, con.Length)
    Assert.Equal(hBearing.Text, con.Head.Target.Id)
    Assert.Equal($"#/investigations/{investigation.Text}/hypotheses?item={hBearing.Text}", con.Head.Href)

[<Fact>]
let ``VPO-34334672f9ff0014 untrusted text is cleaned, only safe http(s) addresses become links, and missing provenance is labelled`` () =
    let photo = inspected (refOf EvidenceRecord ePhoto)
    Assert.Equal(Some "https://example.org/p7/seal-photo.jpg", photo.SafeUrl)
    let captured = value photo "Captured at"
    Assert.True(captured.Missing)
    Assert.Equal("Capture time not recorded", captured.Value)
    // Negative controls: script, data and credential-bearing addresses are never links; control and direction
    // override characters are removed from text.
    Assert.Equal(None, safeUrl "javascript:alert(1)")
    Assert.Equal(None, safeUrl "data:text/html,<script>alert(1)</script>")
    Assert.Equal(None, safeUrl "https://user:secret@example.org/x")
    Assert.Equal(None, safeUrl "/relative/path")
    Assert.Equal("abc<b>x</b>", clean "a‮b\u0000c<b>x</b>")

[<Fact>]
let ``VPO-5a85f3ea85db30b2 VPO-63d89380e8017050 traversal follows only recorded relationships, with stable ids and explicit absent links`` () =
    let s = src.Value
    // Hypothesis -> evidence and cause; cause -> hypotheses and corrective actions; correction -> cause and criterion.
    let seal = inspected (refOf HypothesisRecord hSeal)
    Assert.Equal<string list>([ ePhoto.Text ], (relation seal "Supporting evidence").Links |> List.map (fun l -> l.Target.Id))
    Assert.Equal<string list>([ cause.Text ], (relation seal "Causal relationships").Links |> List.map (fun l -> l.Target.Id))
    Assert.Equal("Root cause", (relation seal "Causal relationships").Links.Head.Relation)
    let c = inspected (refOf CauseRecord cause)
    Assert.Equal<string list>([ hSeal.Text ], (relation c "Hypotheses in it").Links |> List.map (fun l -> l.Target.Id))
    Assert.Equal<string list>([ correction.Text ], (relation c "Corrective actions").Links |> List.map (fun l -> l.Target.Id))
    let ca = inspected (refOf CorrectionRecord correction)
    Assert.Equal<string list>([ cause.Text ], (relation ca "Causes it targets").Links |> List.map (fun l -> l.Target.Id))
    // Timeline events link to and from the records they touched.
    let events = (relation seal "Timeline events").Links
    Assert.NotEmpty events
    let ev = inspected events.Head.Target
    Assert.Contains((relation ev "Records it touched").Links, fun l -> l.Target.Id = hSeal.Text)
    // Absent links are explicit, not an empty list shown as nothing.
    let orphan = inspected (refOf HypothesisRecord hOrphan)
    let none = relation orphan "Supporting evidence"
    Assert.Empty none.Links
    Assert.Equal("No supporting evidence is linked.", none.Absent)
    Assert.Equal("It is not part of any causal relationship.", (relation orphan "Causal relationships").Absent)
    // A selection that names no record resolves to nothing (no invented record).
    Assert.Equal(None, resolve s (Guid.NewGuid().ToString("D")))
    Assert.Equal(Some(refOf HypothesisRecord hSeal), resolve s hSeal.Text)

[<Fact>]
let ``VPO-29cc81617de241fb VPO-141be05c7de03e17 next steps come from the domain, link to their records, and carry the policy verdict`` () =
    let steps = nextSteps src.Value
    let targetOf kind = steps |> List.filter (fun s -> s.Target |> Option.exists (fun t -> t.Kind = kind))
    // Unresolved contradiction and open question (policy obligations), linked to their records.
    let con = targetOf ContradictionRecord |> List.exactlyOne
    Assert.StartsWith("Unresolved contradiction: The trend log", con.Text)
    Assert.Equal($"#/investigations/{investigation.Text}?item={contradiction.Text}", con.Href)
    Assert.True(con.Action.IsSome)
    Assert.Equal(1, (targetOf QuestionRecord).Length)
    // Missing evidence: the hypothesis with no linked evidence, and only that one.
    Assert.Equal<string list>([ hOrphan.Text ], targetOf HypothesisRecord |> List.map (fun s -> (Option.get s.Target).Id))
    // Pending verification: the criterion that has no satisfied verification.
    Assert.Equal(1, (targetOf CriterionRecord).Length)
    // Resolution blockers the policy reports, in words, never as codes.
    Assert.Contains(steps, fun s -> s.Text = "Before resolving: No root cause has been confirmed.")
    for s in steps do
        Assert.DoesNotContain("RESOLVE.", s.Text + s.Why)
    // Each verdict is the policy's: if the step's action is not allowed, the reason is the engine's own.
    let p = src.Value.Projection
    match con.Action with
    | Some(_, allowed, reason) ->
        let cap = p.Capabilities |> List.find (fun c -> c.Action = ContradictionAction(contradiction, ResolveContradiction))
        Assert.Equal(cap.Allowed, allowed)
        if not allowed then Assert.Equal(Language.unavailable cap.Blockers, reason)
    | None -> failwith "no verdict"

[<Fact>]
let ``VPO-141be05c7de03e17 VPO-885e4767b84371de a case with nothing recorded is not shown as having no work, and nothing is invented`` () =
    let s = source (run opening)
    let steps = nextSteps s
    // The policy still says what blocks resolution; no hypothesis or contradiction step is invented.
    Assert.NotEmpty steps
    Assert.DoesNotContain(steps, fun x -> x.Text.StartsWith "Unresolved contradiction" || x.Text.StartsWith "Missing evidence")
    Assert.True(steps |> List.forall (fun x -> x.Text.StartsWith "Before resolving: "))

[<Fact>]
let ``VPO-f8666daf6fdf79d7 VPO-ffda9fea0324a42e a cause's explanation lists supporting and conflicting evidence, hypotheses, history and why it is unresolved`` () =
    let lines = explain src.Value (refOf CauseRecord cause)
    let text = String.concat "\n" lines
    Assert.StartsWith("Status: Proposed, not confirmed.", lines.Head)
    Assert.Contains("Root cause: \"The mechanical seal leaked coolant.\", currently Proposed.", text)
    Assert.Contains("Supporting evidence: \"https://example.org/p7/seal-photo.jpg\" (source: Maintenance photo, shift B).", text)
    Assert.Contains("Conflicting evidence: none is linked.", text)
    Assert.Contains("State history: recorded as Proposed; no change of state is recorded since.", text)
    // Why it is unresolved: exactly the engine's cause gate messages.
    for b in Policy.causeBlockers src.Value.Projection.State cause do
        Assert.Contains("Why it is not confirmed: " + b.Message, text)
    Assert.Contains("Mechanism: not stated.", text)
    Assert.Contains("Corrective action: Replace the mechanical seal with the upgraded type. (Root-cause correction, Proposed).", text)
    // Negative control: no conclusion is fabricated.
    Assert.DoesNotContain("confirmed by the investigator", text)

[<Fact>]
let ``VPO-ad49e5ff43f1ace6 one typed inspector contract serves evidence, hypotheses, causes, corrective actions and timeline events`` () =
    let s = src.Value
    let refs =
        [ refOf EvidenceRecord eTemp; refOf HypothesisRecord hSeal; refOf CauseRecord cause; refOf CorrectionRecord correction
          refOf ContradictionRecord contradiction; refOf QuestionRecord question; refOf CriterionRecord criterion; eventRef 3L ]
    for r in refs do
        let i = inspect s r |> Option.get
        Assert.Equal(r, i.Ref)
        Assert.Equal(kindLabel r.Kind, i.KindLabel)
        Assert.NotEmpty i.Fields
        Assert.NotEmpty i.Relations
        Assert.Equal(Some r, resolve s r.Id)
        for f in i.Fields do Assert.Empty(Language.leaks f.Value)
