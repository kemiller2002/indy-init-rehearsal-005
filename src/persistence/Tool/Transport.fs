/// Performs HttpRequest values with HttpClient: the tool's stand-in for the Limen kernel's Http effect.
module Causa.Persistence.Tool.Transport

open System
open System.Net.Http
open System.Text
open Causa.Persistence

let private client =
    let c = new HttpClient(Timeout = TimeSpan.FromSeconds 60.0)
    c.DefaultRequestHeaders.UserAgent.ParseAdd("causa-persistence-recorder/1.0")
    c

/// Ok response, or the transport failure (NotSent when the request never left, OutcomeUnknown when it may have).
let send (r: HttpRequest) : Result<HttpResponse, TransportFailure> =
    use msg = new HttpRequestMessage(HttpMethod(r.Method), r.Url)
    for (k, v) in r.Headers do
        if not (String.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase)) then
            msg.Headers.TryAddWithoutValidation(k, v) |> ignore
    r.Credential |> Option.iter (fun c -> msg.Headers.TryAddWithoutValidation("Authorization", c.AuthorizationHeader) |> ignore)
    if r.Fresh then msg.Headers.TryAddWithoutValidation("Cache-Control", "no-cache") |> ignore
    r.Body |> Option.iter (fun b -> msg.Content <- new StringContent(b, Encoding.UTF8, "application/json"))
    try
        use resp = client.Send msg
        let body = (new IO.StreamReader(resp.Content.ReadAsStream())).ReadToEnd()
        let headers =
            [ for h in resp.Headers do
                  for v in h.Value -> h.Key.ToLowerInvariant(), v
              for h in resp.Content.Headers do
                  for v in h.Value -> h.Key.ToLowerInvariant(), v ]
        Ok { Status = int resp.StatusCode; Headers = headers; Body = body }
    with
    | :? HttpRequestException as ex when ex.HttpRequestError = HttpRequestError.NameResolutionError || ex.HttpRequestError = HttpRequestError.ConnectionError ->
        Error(NotSent "connection could not be established")
    | :? Threading.Tasks.TaskCanceledException -> Error(OutcomeUnknown "timed out after sending")
    | :? HttpRequestException -> Error(OutcomeUnknown "connection lost")
