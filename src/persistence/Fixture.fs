namespace Causa.Persistence

open System
open IndyInitRootCauseInvestigator.Engine

/// Local fixture mode (PF-003): a synthetic data repository generated in the page, with no network and no
/// credential. It is the same typed port and the same program as GitHub (PF-001): RecordedGitHub answers the
/// program's HttpRequests in the recorded GitHub forms, and every investigation it holds is decided command by command
/// by the engine (Kernel.submit) and written by the engine's storage writer, so it loads through Storage.read like real
/// data (PF-002, PF-004). Nothing here is a demonstration record: the investigation is invented, generated at run
/// time, and never one of the R001 cases or the historical catalog (EXT-DATA-001).
///
/// The application allows it only in the test build (PersistencePort.enableFixtureMode), and only when the person
/// connects to the reserved repository `local/fixture` (branch `main`, workspace `synthetic-fixture`). Command:
///   npm run build:test && node scripts/serve.mjs obj/site-test 4322   (then Connect to local/fixture)
[<RequireQualifiedAccess>]
module Fixture =
    let owner = "local"
    let repo = "fixture"
    let branch = "main"
    let workspace = "synthetic-fixture"

    /// The reserved fixture repository: never a real GitHub repository the application would contact.
    let isFixture (t: Target) = t.Owner = owner && t.Repo = repo

    let private ns = Guid "8f6a3c1e-1d3b-5e2a-9c4b-2f1e0d9c8b7a"
    let private gid (name: string) = Hashing.uuidV5 ns ("causa-fixture/" + name)
    let private idOf name : Id<'T> = Id(gid name)

    let private actor: Actor = { ActorId = "fixture-generator"; Kind = System }
    let private at (minute: int) = DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero).AddMinutes(float minute)

    let investigationId = (gid "investigation").ToString("D")

    /// The commands, in order: a compact, complete investigation (PF-006): observations, competing hypotheses,
    /// supporting and contradicting evidence with provenance, an unknown, an assumption, a contradiction, a
    /// prediction, an action and its finding, negative knowledge, a causal relation, a corrective action with its
    /// criterion, and hypothesis states in several colours.
    let private commands: (string * Command) list =
        let h1, h2, h3 = idOf "h/config", idOf "h/pool", idOf "h/network"
        let e1, e2, e3, e4 = idOf "e/diff", idOf "e/traffic", idOf "e/pool-metrics", idOf "e/latency"
        let cause = idOf "cause/config"
        let correction = idOf "correction/config"
        let action = idOf "action/replay"
        [ "start", StartInvestigation("Synthetic fixture: checkout service returned errors", idOf "o/first", "Checkout requests returned HTTP 500 for 12 minutes after the 14:02 deploy.")
          "activate", ActivateInvestigation
          "o/rollback", RecordObservation(idOf "o/rollback", "The error rate returned to baseline two minutes after the rollback.", Some(at -60))
          "h/config", RecordHypothesis(h1, "The deploy removed the payment timeout setting.", Some "Without a timeout, slow payment calls held request threads until they failed.")
          "h/pool", RecordHypothesis(h2, "A traffic spike exhausted the database connection pool.", None)
          "h/network", RecordHypothesis(h3, "A network partition between checkout and payments.", None)
          "e/diff", AdmitEvidence(e1, TextInput "The deploy diff removes payment.timeout from the service configuration.", Document, "deploy log (synthetic)", Some(at -90))
          "e/traffic", AdmitEvidence(e2, TextInput "Request volume stayed within the normal daily range during the incident.", Measurement, "metrics dashboard (synthetic)", Some(at -80))
          "e/pool-metrics", AdmitEvidence(e3, TextInput "Connection pool usage peaked at 41 percent.", Measurement, "database metrics (synthetic)", Some(at -79))
          "e/latency", AdmitEvidence(e4, UrlInput "https://example.invalid/synthetic/latency-chart", Measurement, "latency chart (synthetic)", None)
          "r/diff-config", RelateEvidence(idOf "r/diff-config", e1, h1, Supports, Some "The setting the hypothesis names was removed by this deploy.")
          "r/traffic-pool", RelateEvidence(idOf "r/traffic-pool", e2, h2, Contradicts, Some "There was no spike.")
          "r/pool-pool", RelateEvidence(idOf "r/pool-pool", e3, h2, Contradicts, Some "The pool was far from exhausted.")
          "r/latency-network", RelateEvidence(idOf "r/latency-network", e4, h3, Insufficient, Some "The chart cannot distinguish a partition from slow calls.")
          "assess/diff", AssessEvidence(e1, Verified)
          "h/config/plausible", TransitionHypothesis(h1, MarkPlausible, "Supported by the deploy diff.", [])
          "h/config/supported", TransitionHypothesis(h1, MarkSupported, "The deploy diff shows the setting removed.", [ e1 ])
          "h/pool/plausible", TransitionHypothesis(h2, MarkPlausible, "Plausible from the first reports.", [])
          "h/pool/weakened", TransitionHypothesis(h2, MarkWeakened, "Traffic and pool metrics contradict it.", [ e2; e3 ])
          "u/other-readers", IdentifyUnknown(idOf "u/other-readers", "Did any other service read the removed setting?", Material)
          "a/single-deploy", RecordAssumption(idOf "a/single-deploy", "Only one deploy happened in the incident window.", Informational)
          "c/latency", RecordContradiction(idOf "c/latency", "Latency rose before the error rate did.", [ e4; e2 ], NonMaterial)
          "p/restore", RecordPrediction(idOf "p/restore", h1, "Restoring the timeout in staging removes the 500s under the same load.", Expected, Prospective)
          "action/replay", RecordAction(action, Experiment, "Replay the incident load in staging with the timeout restored.", [ h1 ])
          "nk/dns", EstablishNegativeKnowledge(idOf "nk/dns", "DNS resolution was not involved.", "checkout to payments", [ e3 ])
          "cause/config", RecordCause(cause, "Checkout returned HTTP 500", [ { Hypothesis = h1; Role = RootCause } ], Some "Removed timeout held threads until the pool of request workers was exhausted.")
          "correction/config", RecordCorrectiveAction(correction, Correction, "Restore the timeout and validate configuration against a schema before deploy.", [ cause ])
          "criterion/config", RecordCriterion(idOf "criterion/config", correction, "No timeout-related 500s for seven days after the change.", Some "error-rate monitor") ]

    /// The synthetic investigation, decided by the engine and written by its storage writer. Every command must be
    /// admitted: a refusal is reported with the engine's reason, never skipped.
    let investigation () : Result<Verified, string> =
        let envelope (i: int) (label: string) (version: int64) (command: Command) : CommandEnvelope =
            { CommandId = gid ("command/" + label)
              Investigation = Id(gid "investigation")
              ExpectedVersion = Some version
              Actor = actor
              RequestedAt = at i
              Command = command }
        match commands with
        | [] -> Error "no commands"
        | (label, first) :: rest ->
            match Store.create (envelope 0 label 0L first) with
            | Error r -> Error(label + ": " + String.Join(" ", r.Reasons))
            | Ok v ->
                rest
                |> List.mapi (fun i x -> i + 1, x)
                |> List.fold (fun acc (i, (label, command)) ->
                    acc
                    |> Result.bind (fun (v: Verified) ->
                        Store.submit v (envelope i label v.Version command)
                        |> Result.mapError (fun r -> label + ": " + r.Code + " " + String.Join(" ", r.Reasons)))) (Ok v)

    /// The in-page provider: the recorded-behaviour GitHub holding the synthetic workspace. No credential is needed
    /// and none is sent anywhere: nothing leaves the page.
    let provider () : Result<RecordedGitHub, string> =
        investigation ()
        |> Result.map (fun v ->
            let p = RecordedGitHub(owner, repo, branch, false)
            p.AnyCredential <- true
            let t = { Owner = owner; Repo = repo; Branch = branch; Workspace = workspace }
            p.CommitFiles(branch, Store.files v |> List.map (fun (n, b) -> Target.investigationDir t v.InvestigationId + "/" + n, b), "fixture: synthetic investigation")
            |> ignore
            p)
