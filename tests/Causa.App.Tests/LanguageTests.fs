/// UI-024: the centralized presentation-language mapping. Test names carry the obligation ids they prove.
module Causa.App.Tests.LanguageTests

open System
open Xunit
open Microsoft.FSharp.Reflection
open IndyInitRootCauseInvestigator.Engine
open Causa.App
open Causa.App.Tests.SyntheticCase

/// Every case of a field-less union, by reflection, so a new domain state cannot slip past these tests.
let private cases<'T> () =
    FSharpType.GetUnionCases typeof<'T> |> Array.map (fun c -> FSharpValue.MakeUnion(c, [||]) :?> 'T) |> List.ofArray

let private caseName (x: 'T) = (FSharpValue.GetUnionFields(x, typeof<'T>) |> fst).Name

/// A label is "raw" when it is the type's own case name in PascalCase (e.g. AwaitingEvidence, CauseProposed).
let private isRaw (x: 'T) (label: string) =
    let name = caseName x
    name.Length > 1 && Seq.exists Char.IsUpper (name.Substring 1) && label.Contains name

let private assertMapped (label: 'T -> string) =
    let all = cases<'T> ()
    Assert.NotEmpty all
    for x in all do
        let l = label x
        Assert.False(String.IsNullOrWhiteSpace l, $"{caseName x} has no wording")
        Assert.False(isRaw x l, $"{caseName x} is shown as its raw case name: {l}")
        Assert.Empty(Language.leaks l)

[<Fact>]
let ``VPO-d8ac36bdb18a7b39 every typed domain state has one concise user-facing label, none raw or technical`` () =
    assertMapped Language.lifecycle
    assertMapped Language.lifecycleExplanation
    assertMapped Language.hypothesis
    assertMapped Language.cause
    assertMapped Language.causeRole
    assertMapped Language.evidenceCategory
    assertMapped Language.evidenceAssessment
    assertMapped Language.relationship
    assertMapped Language.materiality
    assertMapped Language.unknownState
    assertMapped Language.contradictionState
    assertMapped Language.assumptionState
    assertMapped Language.correctiveKind
    assertMapped Language.correctiveState
    assertMapped Language.actionState
    assertMapped Language.verificationOutcome
    assertMapped Language.conclusionStrength
    assertMapped Language.proposalState

[<Fact>]
let ``VPO-d8ac36bdb18a7b39 the shell's wire-spelling labels agree with the typed mapping`` () =
    for l in cases<Lifecycle> () do
        Assert.Equal(Language.lifecycle l, Presentation.label (EngineView.lifecycle l))
    for h in cases<HypothesisState> () do
        Assert.Equal(Language.hypothesis h, Presentation.label (EngineView.hypothesisState h))
    for c in cases<EvidenceCategory> () do
        Assert.Equal(Language.evidenceCategory c, Presentation.label (EngineView.category c))

[<Fact>]
let ``VPO-e8570ba5427ce32b views cannot invent a permission, a reason or a recommendation`` () =
    // No reason from the engine: the words say so, and never imply the action is allowed.
    let none = Language.unavailable []
    Assert.Equal("Not available now; no reason was given.", none)
    // A code the mapping does not know is explained only by the engine's own message.
    let e = { Code = "SOMETHING.NEW"; Rule = "x"; Message = "The engine's own reason." }
    Assert.Equal("The engine's own reason.", Language.nextStepFor e)
    Assert.Equal("The engine's own reason.", Language.unavailable [ e ])
    // The next legal action on a card is exactly what the F# policy allows: a draft offers activation, an active
    // investigation with no confirmed cause never offers "Resolve".
    let draft = run [ StartInvestigation("A draft", obs, "Something happened.") ] |> projectionOf
    Assert.Equal(Some "Activate the investigation", Records.nextLegalAction draft)
    let active = run opening |> projectionOf
    Assert.NotEqual(Some "Resolve the investigation", Records.nextLegalAction active)
    Assert.DoesNotContain(active.Capabilities, fun c -> c.Action = LifecycleAction ResolveInvestigationCommand && c.Allowed)

[<Fact>]
let ``VPO-e8570ba5427ce32b VPO-885e4767b84371de findings wording never claims a confirmed cause or verification the domain does not assert`` () =
    let p = run full |> projectionOf
    let words = Language.findings p.Verification
    Assert.Equal<string list>([ "No cause is confirmed."; "No correction is verified effective."; "Not resolved." ], words)
    // Negative control: data that was read and checked is still not a finding.
    let checkedOk = Language.integrity (Some { EventsSha256 = "a"; StateSha256 = "b"; AdmissibilityChecked = true })
    Assert.DoesNotContain("Verified", checkedOk)
    Assert.DoesNotContain("verified", checkedOk)

[<Fact>]
let ``VPO-6f0a2e18f9a19003 wording never implies an unsaved, ephemeral, read-only or in-flight change was saved`` () =
    let saved (s: string) = s.Contains("Saved", StringComparison.Ordinal) || s.StartsWith("saved", StringComparison.OrdinalIgnoreCase)
    for d in [ Language.ReadOnlyDemonstration; Language.ThisTabOnly; Language.NotYetSaved 1; Language.NotYetSaved 3 ] do
        Assert.False(saved (Language.durability d), Language.durability d)
    Assert.Equal("Saved and confirmed at 2026-03-01 09:00 UTC.", Language.durability (Language.Persisted(Some "2026-03-01 09:00 UTC")))
    let change: Sync.Change = { DecidedAt = Sync.Oid "a1"; Marker = "m"; Files = [] }
    let attempt: Sync.Attempt = { Request = Sync.RequestId 1L; Change = change; Stage = Sync.Sent }
    let phases =
        [ Sync.Disconnected
          Sync.Idle(Sync.Oid "a1", Some change)
          Sync.Writing(Sync.Oid "a1", attempt)
          Sync.Confirming(Sync.Oid "a1", attempt, Sync.Oid "b2", Sync.RequestId 2L)
          Sync.Unknown(Sync.Oid "a1", attempt)
          Sync.Reconciling(Sync.Oid "a1", attempt, Sync.RequestId 3L)
          Sync.Held(Sync.Oid "a1", change, Sync.Conflict None)
          Sync.Held(Sync.Oid "a1", change, Sync.NotWritten(Sync.NeverSent Sync.TimedOut)) ]
    for ph in phases do
        let w = Language.syncPhase ph
        Assert.False(saved w && not (w.Contains "not saved" || w.Contains "Not saved" || w.Contains "may or may not"), w)
        Assert.DoesNotContain("Saved to", w)
    Assert.Contains("may or may not", Language.syncPhase (Sync.Unknown(Sync.Oid "a1", attempt)))
    Assert.Contains("Your changes are kept", Language.syncPhase (Sync.Held(Sync.Oid "a1", change, Sync.Conflict None)))
    // Commit ids never appear in the wording.
    for ph in phases do
        Assert.DoesNotContain("a1", Language.syncPhase ph)

[<Fact>]
let ``VPO-e1fb14be4ca10f5f technical codes and data-integrity details stay out of routine wording`` () =
    let p = run full |> projectionOf
    // The policy's blockers carry codes such as RESOLVE.NO_CONFIRMED_CAUSE; the words shown never do.
    Assert.NotEmpty p.ResolutionBlockers
    for b in p.ResolutionBlockers do
        let w = Language.nextStepFor b
        Assert.DoesNotContain(b.Code, w)
        Assert.Empty(Language.leaks w)
    // Integrity is the Administration vocabulary: it is flagged as technical if it reaches routine copy.
    Assert.NotEmpty(Language.leaks (Language.integrity (Some { EventsSha256 = "a"; StateSha256 = "b"; AdmissibilityChecked = true })))
    Assert.Contains("SHA", Language.leaks "events SHA-256 1f2e")
    Assert.Contains("replayed", Language.leaks "Loaded from catalog and replayed")
    Assert.Empty(Language.leaks "Informational; a formal commitment")

[<Fact>]
let ``VPO-7fff10ce82e7c162 domain terms and provenance are kept precisely`` () =
    Assert.Equal("Root cause", Language.causeRole RootCause)
    Assert.Equal("Contributing factor", Language.causeRole ContributingFactor)
    Assert.Equal("Enabling condition", Language.causeRole EnablingCondition)
    Assert.Equal("Root-cause correction", Language.correctiveKind Correction)
    Assert.Equal("Mitigation", Language.correctiveKind Mitigation)
    Assert.Equal("Accepted as residual uncertainty", Language.unknownState AcceptedResidual)
    Assert.Equal("Implemented, effect not verified", Language.correctiveState Implemented)
    // Provenance: an agent is always named as an agent with its provider and model; times are labelled UTC.
    Assert.Equal("causa-ai (AI agent, anthropic model-x)", Language.actor { ActorId = "causa-ai"; Kind = Agent("anthropic", "model-x") })
    Assert.Equal("investigator-a", Language.actor person)
    Assert.Equal("2026-03-01 09:00 UTC", Language.stamp at0)
