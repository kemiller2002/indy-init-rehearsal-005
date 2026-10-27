/// The Indy-init verification plan (indy-init.verification-plan/1), read-only.
module Causa.Acceptance.Plan

open Causa.Acceptance.Json

type Obligation =
    { Id: string
      SourceObligation: string
      Text: string
      Requirement: string }

type Requirement =
    { Id: string
      Priority: string
      Title: string
      Document: string
      Gates: string list
      Freshness: string
      Obligations: Obligation list }

type VerificationPlan =
    { PlanId: string
      Digest: string
      BaselineId: string
      BaselineSha256: string
      BaselineRepository: string
      BaselineCommit: string
      ApprovalsSha256: string
      Requirements: Requirement list }

let ofJson (plan: System.Text.Json.Nodes.JsonNode) : VerificationPlan =
    let identity = field "planIdentity" plan
    let source = field "source" plan
    let sourceStr name = source |> Option.bind (str name) |> Option.defaultValue ""
    let approvals = source |> Option.bind (field "approvals")

    let requirement (r: System.Text.Json.Nodes.JsonNode) =
        let rid = strOr "" "requirement" r

        { Id = rid
          Priority = strOr "" "priority" r
          Title = strOr "" "title" r
          Document = strOr "" "document" r
          Gates = field "verificationMethod" r |> Option.map (strings "gates") |> Option.defaultValue []
          Freshness = field "freshness" r |> Option.bind (str "policy") |> Option.defaultValue ""
          Obligations =
            items "obligations" r
            |> List.map (fun o ->
                { Id = strOr "" "id" o
                  SourceObligation = strOr "" "sourceObligation" o
                  Text = strOr "" "text" o
                  Requirement = rid }) }

    { PlanId = identity |> Option.bind (str "planId") |> Option.defaultValue ""
      Digest = identity |> Option.bind (str "digest") |> Option.defaultValue ""
      BaselineId = sourceStr "baselineId"
      BaselineSha256 = sourceStr "baselineSha256"
      BaselineRepository = sourceStr "repository"
      BaselineCommit = sourceStr "commit"
      ApprovalsSha256 = approvals |> Option.bind (str "sha256") |> Option.defaultValue ""
      Requirements = items "requirements" plan |> List.map requirement }

let load path = Json.load path |> ofJson

let obligations (plan: VerificationPlan) = plan.Requirements |> List.collect (fun r -> r.Obligations)
