/// The one stateful edge between the Limen kernel and the application: holds the model between WebAssembly calls,
/// supplies entropy (fresh ids, the time, the page-load nonce) and speaks JSON through the generated codec.
module Causa.App.Dispatch

open System
open Limen.Contract.Core
open Causa.App.Adapter
open Causa.App.Shell

/// Unique per page load: fresh-read URLs never repeat across sessions or reloads.
let private nonce = Guid.NewGuid().ToString("N").Substring(0, 12)

let private entropy () =
    { Now = DateTime.UtcNow
      Ids = List.init 48 (fun _ -> Guid.NewGuid())
      Nonce = nonce }

let mutable private state = initial
let mutable private testHooks = false

/// Test build only (scripts/build-site.mjs --test): accept the Percepta state event.
let enableTestHooks () = testHooks <- true

/// Percepta state injection: replaces the port with a fixed projection of the injected state, so the normal View
/// renders it. Refused (unchanged model, a recorded reason) in the production build or for an unsupported state.
let private inject (e: SemanticEvent) (model: Model) =
    if not testHooks then model
    else
        match TestState.parse (e.Value |> Option.defaultValue "") with
        | Ok s when s.Name = "engine-memory" ->
            // The real kernel over an in-memory workspace (T17 checkpoint); nothing is saved anywhere.
            { model with Port = EnginePort.create (EnginePort.Persistence.memory Map.empty []); InjectedState = Some s.Name; Notice = None }
        | Ok s -> { model with Port = Port.fixedAt (TestState.toProjection s); InjectedState = Some s.Name; Notice = None }
        | Error reason -> { model with InjectedState = Some("refused: " + reason) }

let step (entropy: Entropy) (message: BrowserToEngineMessage) (model: Model) =
    match message with
    | BrowserToEngineMessage.Event e when e.Name = "percepta-state" -> inject e model, []
    | _ -> update entropy message model

let handle (json: string) : string =
    match Codec.parseBrowserToEngineMessage json with
    | Error error ->
        // A message outside the contract is a compatibility failure, not an application event.
        failwith ("Browser message outside the Limen contract at " + error.Path + ": expected " + error.Expected + ", found " + error.Found)
    | Ok message ->
        let handshake =
            match message with
            | BrowserToEngineMessage.Initialize(_, _, offer) -> Some(Limen.Guest.Handshake.answer offer Limen.Guest.Handshake.coreOnly)
            | _ -> None

        // An internal failure while handling one message must not take the page down: the model is kept and the
        // person is told in plain words; the technical cause is not shown in the workflow (UI-017-O4).
        let next, effects =
            try
                step (entropy ()) message state
            with ex ->
                { state with Notice = Some { Tone = "danger"; Text = "Something went wrong while handling that action. Nothing was changed because of it." } },
                []

        state <- next

        { View = View.project next
          Effects = effects
          Cancellations = []
          Handshake = handshake }
        |> Codec.serializeEngineToBrowserMessage

/// For tests: start again from the initial model.
let reset () =
    state <- initial
    testHooks <- false
