/// T16: the route codec round-trips, so a deep link survives reload. Test names carry the obligation ids they prove.
module Causa.App.Tests.RouteTests

open System
open Xunit
open Causa.App.Routes

let private ids =
    let random = Random 20261027
    List.init 50 (fun _ -> Guid(Array.init 16 (fun _ -> byte (random.Next 256))).ToString("D"))
    |> List.filter isInvestigationId

let private allRoutes =
    [ Home; Investigations; NewInvestigation; Administration; Settings ]
    @ [ for id in ids do
            for (s, _, _) in sections -> Investigation(id, s) ]

[<Fact>]
let ``VPO-adf482d16e75a882 every route round-trips through its hash`` () =
    for r in allRoutes do
        Assert.Equal(r, parse (format r))

[<Fact>]
let ``VPO-adf482d16e75a882 formatting a parsed canonical hash gives the same hash`` () =
    for r in allRoutes do
        let h = format r
        Assert.Equal(h, format (parse h))

[<Fact>]
let ``unknown or malformed addresses are a NotFound value, never an exception`` () =
    for h in [ "#/x"; "#/investigations/not-a-guid"; "#/investigations/00000000-0000-0000-0000-000000000000"; "#/investigations/ABCDEF01-0000-4000-8000-000000000001"; "#/investigations/" + ids.Head + "/nope"; "#//"; "x" ] do
        match parse h with
        | NotFound _ -> ()
        | other -> failwithf "%s parsed as %A" h other

[<Fact>]
let ``VPO-7bb1e8c5bdccc2f2 the three primary destinations are distinct routes`` () =
    Assert.Equal(InvestigationsArea, primaryOf Investigations)
    Assert.Equal(AdministrationArea, primaryOf Administration)
    Assert.Equal(SettingsArea, primaryOf Settings)
    Assert.Equal(InvestigationsArea, primaryOf (Investigation(ids.Head, Timeline)))

[<Fact>]
let ``VPO-628a11b571e65796 an investigation has the six required sections in order`` () =
    let names = sections |> List.map (fun (_, _, n) -> n)
    Assert.Equal<string list>([ "Overview"; "Causal analysis"; "Evidence"; "Hypotheses"; "Timeline"; "Corrective actions"; "Report"; "AI proposals" ], names)

[<Fact>]
let ``a query selects one record only when the id is safe`` () =
    let r = Investigation(ids.Head, Evidence)
    Assert.Equal("abc-1", selection (formatItem r "abc-1"))
    Assert.Equal("", selection (format r + "?item=<script>"))
    Assert.Equal("", selection (format r))
