/// Immutable model of the inputs and outputs of the identity derivation.
module DeriveRequirements.Domain

type ObligationKind =
    | Value
    | Acceptance
    | Constraint

type Obligation =
    { Id: string
      Kind: ObligationKind
      Text: string
      Tokens: string list }

type Requirement =
    { Id: string
      Title: string
      Priority: string
      Document: string
      Obligations: Obligation list }

type Baseline =
    { Id: string
      Commit: string
      Sha256: string
      Requirements: Requirement list }

/// The normalized-requirements entry a baseline requirement is carried under.
type NormalizedEntry = { Id: string; Source: string; Title: string }

/// An operator-recorded equivalence review covering a set of obligation ids.
type ReviewRecord =
    { Reviewer: string
      Date: string
      Basis: string
      Obligations: string list }

type Review = { Reviewer: string; Date: string; Basis: string }

/// One derived criterion: the row a mapping points at.
type Criterion =
    { Id: string
      Text: string
      IsAcceptance: bool }

type DerivedDocument =
    { Id: string
      Source: string
      Title: string
      Priority: string
      FileName: string
      Criteria: Criterion list }

type Mapping =
    { Obligation: string
      Criteria: string list
      Review: Review option }

type Disposition = { Requirement: string; Mappings: Mapping list }

/// A token-less obligation the gate cannot decide by tokens or verbatim carriage.
type ReviewRequest =
    { Requirement: string
      Obligation: string
      Criterion: string
      Text: string
      CriterionText: string
      ByteIdentical: bool }

let kindOfString (s: string) =
    match s with
    | "value" -> Ok Value
    | "acceptance" -> Ok Acceptance
    | "constraint" -> Ok Constraint
    | other -> Error $"unknown obligation kind '{other}'"
