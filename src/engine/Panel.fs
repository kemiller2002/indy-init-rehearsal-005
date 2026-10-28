namespace IndyInitRootCauseInvestigator.Engine

open System

/// One specialised, blind analysis role (S17). Its instructions are a fixed, versioned platform text: investigation
/// content and other roles' outputs never enter them.
type PanelRole =
    { Id: string
      Name: string
      Focus: string
      PromptVersion: string }

/// Where a draft came from: every panel or tournament draft keeps this provenance (S17, S18).
type DraftProvenance =
    { Role: string option
      Treatment: string option
      Provider: string
      Model: string
      PromptVersion: string
      SourceVersion: int64 }

/// One answer: the classified result, with its provenance and the latency the shell measured.
type PanelAnswer =
    { Provenance: DraftProvenance
      Result: AiResult
      LatencyMs: int64 option }

/// A structural difference between roles' drafts. Never a vote: who proposed it and who answered without it.
type Disagreement =
    { Kind: ProposalKind
      Text: string
      ProposedBy: string list
      /// Roles that answered but did not propose it (an omission, not a rejection).
      OmittedBy: string list
      /// Roles that proposed the same text as a different kind (e.g. a hypothesis versus an open question).
      OtherKindBy: (string * ProposalKind) list }

/// Several requests in flight against one frozen investigation version.
type BatchState =
    { Investigation: InvestigationId
      SourceVersion: int64
      Issued: int64
      Pending: Map<int64, AiPending * DraftProvenance>
      Answers: PanelAnswer list }

/// A tournament treatment (S18): a model id and a prompt version, run on a frozen source state.
type Treatment =
    { Id: string
      Model: string
      PromptVersion: string }

/// Measured, per treatment. No score and no winner: these are the observations only.
type TreatmentMetrics =
    { Treatment: string
      Model: string
      PromptVersion: string
      SourceVersion: int64
      Answered: bool
      ValidOutput: bool option
      Drafts: int
      Refused: bool
      LatencyMs: int64 option
      Usage: AiUsage option
      Outcome: string }

type BatchFault =
    | UnknownPromptVersion of string
    | NoRoles
    | NoTreatments
    | DuplicateTreatment of string

/// The multi-agent panel (S17) and research tournament (S18) on top of AiLoop. Pure: effects out, outcomes in.
/// Every request is built from the same frozen version before any answer exists, so no output can reach another
/// request's prompt. All drafts remain proposals that a human accepts or rejects (D-004).
[<RequireQualifiedAccess>]
module Panel =
    let private role id name focus = { Id = id; Name = name; Focus = focus; PromptVersion = $"causa.panel-role/{id}/1" }

    /// The fixed, versioned role set.
    let roles =
        [ role "evidence-auditor" "Evidence auditor" "Check whether each hypothesis is actually supported by the recorded evidence, and propose open questions where support is missing or weak."
          role "alternative-cause-seeker" "Alternative-cause seeker" "Propose plausible alternative causes the investigation has not yet considered, and the questions that would distinguish them."
          role "timeline-checker" "Timeline checker" "Check the order and timing of the recorded observations and evidence, and propose open questions or actions where the sequence is unclear or inconsistent." ]

    /// Instructions for a role: the platform instructions plus the role's fixed focus.
    let instructionsFor (r: PanelRole) =
        AiLoop.instructions + "\nYour specialised role: " + r.Name + ". " + r.Focus + "\nYou work independently; you do not see any other analysis."

    /// Every prompt version the tournament may use, with its fixed instructions.
    let prompts : (string * string) list =
        (AiLoop.promptPolicyVersion, AiLoop.instructions) :: (roles |> List.map (fun r -> r.PromptVersion, instructionsFor r))

    let private providerName (p: AiProvider) = match p with Anthropic -> "anthropic"

    let private launch (key: ProviderKey) (inv: Investigation) (task: string) (requests: (string * AiProfile * DraftProvenance) list) =
        let effects, pending =
            requests
            |> List.mapi (fun i (system, profile, prov) ->
                let request = int64 (i + 1)
                let p: AiPending = { Request = request; Investigation = inv.Id; ContextVersion = inv.Version; Profile = profile }
                AiLoop.requestWith request key system profile inv task, (request, (p, prov)))
            |> List.unzip
        { Investigation = inv.Id
          SourceVersion = inv.Version
          Issued = int64 requests.Length
          Pending = Map.ofList pending
          Answers = [] },
        effects

    /// One request per role against the same investigation version.
    let startPanel (key: ProviderKey option) (profile: AiProfile) (inv: Investigation) (task: string) (panel: PanelRole list)
        : Result<BatchState * AiHttpEffect list, Choice<AiResult, BatchFault>> =
        match key, panel with
        | None, _ -> Error(Choice1Of2 KeyMissing)
        | _, [] -> Error(Choice2Of2 NoRoles)
        | Some k, _ ->
            panel
            |> List.map (fun r ->
                instructionsFor r,
                profile,
                { Role = Some r.Id; Treatment = None; Provider = providerName profile.Provider; Model = profile.Model; PromptVersion = r.PromptVersion; SourceVersion = inv.Version })
            |> launch k inv task
            |> Ok

    /// One request per treatment against the same frozen investigation version.
    let startTournament (key: ProviderKey option) (baseProfile: AiProfile) (inv: Investigation) (task: string) (treatments: Treatment list)
        : Result<BatchState * AiHttpEffect list, Choice<AiResult, BatchFault>> =
        let ids = treatments |> List.map (fun t -> t.Id)
        match key, treatments with
        | None, _ -> Error(Choice1Of2 KeyMissing)
        | _, [] -> Error(Choice2Of2 NoTreatments)
        | _, _ when (ids |> List.distinct).Length <> ids.Length ->
            Error(Choice2Of2(DuplicateTreatment(ids |> List.countBy id |> List.find (fun (_, n) -> n > 1) |> fst)))
        | Some k, _ ->
            match treatments |> List.tryFind (fun t -> not (prompts |> List.exists (fun (v, _) -> v = t.PromptVersion))) with
            | Some t -> Error(Choice2Of2(UnknownPromptVersion t.PromptVersion))
            | None ->
                treatments
                |> List.map (fun t ->
                    let profile = { baseProfile with Model = t.Model }
                    prompts |> List.find (fun (v, _) -> v = t.PromptVersion) |> snd,
                    profile,
                    { Role = None; Treatment = Some t.Id; Provider = providerName profile.Provider; Model = t.Model; PromptVersion = t.PromptVersion; SourceVersion = inv.Version })
                |> launch k inv task
                |> Ok

    /// Record the outcome of one pending request, with the latency the shell measured. Unknown or repeated answers are
    /// refused and change nothing.
    let receive (state: BatchState) (outcome: AiHttpOutcome) (latencyMs: int64 option) : Result<BatchState, AiLoopFault> =
        let request = AiLoop.requestOfOutcome outcome
        match state.Pending |> Map.tryFind request with
        | None -> Error(StaleAnswer request)
        | Some(pending, prov) ->
            Ok
                { state with
                    Pending = state.Pending.Remove request
                    Answers = state.Answers @ [ { Provenance = prov; Result = AiLoop.classify pending outcome; LatencyMs = latencyMs } ] }

    let complete (state: BatchState) = state.Pending.IsEmpty

    let private normalise (s: string) =
        String.Join(" ", s.ToLowerInvariant().Split([| ' '; '\t'; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.')

    let private label (a: PanelAnswer) = a.Provenance.Role |> Option.orElse a.Provenance.Treatment |> Option.defaultValue "unnamed"

    /// Drafts per answering role (roles whose request produced drafts; others did not answer and are not counted).
    let drafts (state: BatchState) =
        state.Answers
        |> List.choose (fun a ->
            match a.Result with
            | Drafted(ds, _, _) -> Some(label a, ds)
            | _ -> None)

    /// Structural disagreements: each distinct draft that not every answering role proposed, or that roles proposed
    /// as different kinds. Items every answering role proposed as the same kind are agreements and are not listed.
    let disagreements (state: BatchState) : Disagreement list =
        let answered = drafts state
        let names = answered |> List.map fst
        let all =
            answered
            |> List.collect (fun (r, ds) -> ds |> List.map (fun d -> normalise d.Text, (r, d)))
        all
        |> List.groupBy fst
        |> List.collect (fun (_, group) ->
            let byKind = group |> List.map snd |> List.groupBy (fun (_, d) -> d.Kind)
            let proposers = group |> List.map (fun (_, (r, _)) -> r) |> List.distinct
            byKind
            |> List.choose (fun (kind, items) ->
                let these = items |> List.map fst |> List.distinct
                let others = group |> List.map snd |> List.filter (fun (_, d) -> d.Kind <> kind) |> List.map (fun (r, d) -> r, d.Kind) |> List.distinct
                let omitted = names |> List.filter (fun n -> not (List.contains n proposers))
                if omitted.IsEmpty && others.IsEmpty then None
                else
                    Some
                        { Kind = kind
                          Text = (items |> List.head |> snd).Text
                          ProposedBy = these
                          OmittedBy = omitted
                          OtherKindBy = others }))

    /// Drafts every answering role proposed as the same kind (agreement is recorded, never treated as truth).
    let agreements (state: BatchState) : (ProposalKind * string) list =
        let answered = drafts state
        let n = answered.Length
        if n < 2 then []
        else
            answered
            |> List.collect (fun (r, ds) -> ds |> List.map (fun d -> (d.Kind, normalise d.Text), (r, d.Text)))
            |> List.groupBy fst
            |> List.choose (fun ((kind, _), xs) -> if (xs |> List.map (fun (_, (r, _)) -> r) |> List.distinct).Length = n then Some(kind, xs |> List.head |> snd |> snd) else None)

    /// Per-treatment observations (S18). No winner and no combined score.
    let metrics (state: BatchState) : TreatmentMetrics list =
        state.Answers
        |> List.map (fun a ->
            let valid, count, refused, usage =
                match a.Result with
                | Drafted(ds, _, u) -> Some true, ds.Length, false, u
                | InvalidOutput _
                | Truncated -> Some false, 0, false, None
                | ModelRefused _ -> None, 0, true, None
                | _ -> None, 0, false, None
            { Treatment = label a
              Model = a.Provenance.Model
              PromptVersion = a.Provenance.PromptVersion
              SourceVersion = a.Provenance.SourceVersion
              Answered = (match a.Result with Drafted _ | InvalidOutput _ | Truncated | ModelRefused _ -> true | _ -> false)
              ValidOutput = valid
              Drafts = count
              Refused = refused
              LatencyMs = a.LatencyMs
              Usage = usage
              Outcome = AiLoop.describe a.Result })

    /// What the measurements cannot support, stated explicitly.
    let limitations (state: BatchState) : string list =
        let n = (state.Answers.Length + state.Pending.Count)
        [ $"One investigation at one frozen version ({state.SourceVersion}); results do not generalise to other cases."
          $"One run per treatment ({n} treatment(s)); provider output is not deterministic, so a repeat may differ."
          "There is no ground truth here: draft counts and valid output measure form, not whether a proposal is right."
          "Latency is measured in this browser and includes the network; token counts are as the provider reported them."
          "No treatment is ranked or named the winner; compare the measurements for the question you have." ]
