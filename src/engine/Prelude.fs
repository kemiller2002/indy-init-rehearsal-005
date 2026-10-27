namespace IndyInitRootCauseInvestigator.Engine

/// A minimal `result { }` computation expression (railway composition for decoders and readers).
[<AutoOpen>]
module Prelude =
    type ResultBuilder() =
        member _.Return(x) = Ok x
        member _.ReturnFrom(r: Result<'T, 'E>) = r
        member _.Bind(r: Result<'T, 'E>, f: 'T -> Result<'U, 'E>) = Result.bind f r
        member _.Zero() = Ok()
        member _.Delay(f: unit -> Result<'T, 'E>) = f
        member _.Run(f: unit -> Result<'T, 'E>) = f ()
        member _.Combine(r: Result<unit, 'E>, f: unit -> Result<'T, 'E>) = Result.bind f r

    let result = ResultBuilder()
