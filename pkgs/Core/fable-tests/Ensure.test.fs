module EffSharp.Core.FableTests.EnsureTests

open Fable.Core
open EffSharp.Core
open EffSharp.Core.FableTests
open EffSharp.Core.FableTests.Expect

Vitest.describe "ensure under Fable" <| fun () ->
  Vitest.itp "ensure finalizes the chain it wraps before outer continuation runs" <| fun () -> await {
    let events = ResizeArray<string>()

    let! value =
      (Pure 1 : Eff<int, unit, unit>)
      |> Eff.bind (fun x ->
        Eff.thunk (fun () ->
          events.Add $"body {x}"
          x + 1))
      |> Eff.ensure (Eff.thunk (fun () -> events.Add "cleanup"))
      |> Eff.bind (fun x ->
        Eff.thunk (fun () ->
          events.Add $"next {x}"
          x + 1))
      |> Helpers.run

    equal value (Exit.Ok 3)
    arrEqual (events.ToArray()) [| "body 1"; "cleanup"; "next 2" |]
  }
