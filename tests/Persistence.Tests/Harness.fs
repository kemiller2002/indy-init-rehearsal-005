/// The application runtime used by the application-level tests: the real Sync program, its effects performed against
/// the recorded-behaviour fake GitHub and an in-memory browser store, with deterministic fault injection (PF-009).
module Persistence.Tests.Harness

open Causa.Persistence
open Persistence.Tests.FakeGitHub

type Fault =
    | Deliver
    /// The request never leaves the device.
    | NotSentFault
    /// GitHub applies the request, but the answer is lost.
    | ApplyThenLose
    /// The connection drops before GitHub applies it; the client cannot tell.
    | LoseWithoutApplying
    /// The answer is held back for the test to deliver later (delay/reorder).
    | Hold
    /// GitHub answers with this response instead of the fake (nothing is applied).
    | Answer of HttpResponse

let token = "causa-test-token-7f3a9c"

let owner, repo, branch = "causa-test-owner", "causa-test-data", "main"

let target workspace =
    match Target.create owner repo branch workspace with
    | Ok t -> t
    | Error e -> failwithf "%A" e

let credential (t: string) =
    match Credential.create t with
    | Ok c -> c
    | Error e -> failwith e

let env = { Now = (fun () -> "2026-10-27T12:00:00.000Z"); Nonce = "page-1" }

let mutable private pages = 0

/// One browser tab: each App (and each Reload) is a new page load with its own nonce.
type App(fake: Fake) =
    let mutable env = (pages <- pages + 1; { env with Nonce = sprintf "page-%d" pages })
    let mutable model = Sync.initial
    let mutable store: Map<string, string> = Map.empty
    let mutable queue: Effect list = []
    let mutable refusals: string list = []
    let mutable held: (string * Result<HttpResponse, TransportFailure>) list = []
    let mutable effectsSeen: Effect list = []

    member val Faults: HttpRequest -> Fault = (fun _ -> Deliver) with get, set
    member val StoreFails = false with get, set

    member _.Model = model
    member _.Store = store
    member _.Refusals = refusals
    member _.Effects = effectsSeen
    member _.Held = held

    /// Apply one message. A refusal leaves the model exactly as it was.
    member _.Dispatch(msg: Msg) : Result<unit, string> =
        match Sync.update env msg model with
        | Ok(m, effects) ->
            model <- m
            queue <- queue @ effects
            effectsSeen <- effectsSeen @ effects
            Ok()
        | Error r ->
            refusals <- refusals @ [ r.Reason ]
            Error r.Reason

    /// Perform queued effects until none remain (held answers stay held).
    member this.Run() =
        while not queue.IsEmpty do
            let e = queue.Head
            queue <- queue.Tail
            match e with
            | StoreLocal(writeId, key, value) ->
                if not this.StoreFails then store <- store |> Map.add key value
                this.Dispatch(LocalStored(writeId, not this.StoreFails)) |> ignore
            | Send(id, request) ->
                match this.Faults request with
                | Deliver -> this.Dispatch(HttpAnswered(id, fake.Handle request)) |> ignore
                | NotSentFault -> this.Dispatch(HttpFailed(id, NotSent "offline")) |> ignore
                | ApplyThenLose ->
                    fake.Handle request |> ignore
                    this.Dispatch(HttpFailed(id, TransportFailure.OutcomeUnknown "connection lost after sending")) |> ignore
                | LoseWithoutApplying -> this.Dispatch(HttpFailed(id, TransportFailure.OutcomeUnknown "connection lost")) |> ignore
                | Hold -> held <- held @ [ id, Ok(fake.Handle request) ]
                | Answer resp -> this.Dispatch(HttpAnswered(id, resp)) |> ignore

    member this.Step(msg: Msg) =
        let r = this.Dispatch msg
        this.Run()
        r

    member this.DeliverHeld(id: string) =
        match held |> List.tryFind (fst >> (=) id) with
        | Some(_, outcome) ->
            held <- held |> List.filter (fst >> (<>) id)
            let r =
                match outcome with
                | Ok resp -> this.Dispatch(HttpAnswered(id, resp))
                | Error f -> this.Dispatch(HttpFailed(id, f))
            this.Run()
            r
        | None -> Error "nothing held"

    /// A browser reload: memory (model, credential) is gone; only the store survives.
    member this.Reload() =
        pages <- pages + 1
        env <- { env with Nonce = sprintf "page-%d" pages }
        model <- Sync.initial
        queue <- []
        held <- []
        this.Step(Restore(Map.tryFind Sync.storageKey store))

    member this.Connect(workspace: string, ?tok: string) =
        this.Step(Connect(target workspace, credential (defaultArg tok token)))

    member _.Doc(id: string) = Map.find id model.Documents
    member _.Status(id: string) = Sync.documentStatus model (Map.find id model.Documents)
    member _.Connection = Sync.connectionLabel model

/// A fake data repository with a public read and the test token authorized.
let newFake () =
    let f = Fake(owner, repo, branch, true)
    f.Authorize token
    f
