/// The offline fake GitHub: Causa.Persistence.RecordedGitHub, served only from recordings (T18). Kept as a module of
/// aliases so the tests read as before; the one implementation also backs local fixture mode (PF-001, PF-003).
module Persistence.Tests.FakeGitHub

open Causa.Persistence

type Observation = Recorded.Observation

let observations = Recorded.observations
let recorded (scenario: string) = Recorded.recorded scenario
let respond (scenario: string) (overlay: JValue option) = Recorded.respond scenario overlay
let gitBlobSha (bytes: byte[]) = Recorded.gitBlobSha bytes

type Fake = RecordedGitHub
