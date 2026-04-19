module EffSharp.Core.FableTests.DeferScopeTests

open Fable.Core
open EffSharp.Core
open EffSharp.Core.FableTests
open EffSharp.Core.FableTests.Expect

Vitest.describe "defer scope under Fable" <| fun () ->
  Vitest.itp "defer finalizes after later computation expression steps" <| fun () -> await {
    let events = ResizeArray<string>()

    let! value =
      eff {
        let! x = Pure 1
        defer (Eff.thunk (fun () -> events.Add "cleanup"))
        do! Eff.thunk (fun () -> events.Add "after")
        return x
      }
      |> Helpers.run

    equal value (Exit.Ok 1)
    arrEqual (events.ToArray()) [| "after"; "cleanup" |]
  }

  Vitest.itp "defer finalizes before outer continuation chained after the computation expression" <| fun () -> await {
    let events = ResizeArray<string>()

    let! value =
      (eff {
        let! x = Pure 1
        defer (Eff.thunk (fun () -> events.Add "cleanup"))
        do! Eff.thunk (fun () -> events.Add "after")
        return x
      })
      |> Eff.bind (fun x ->
        Eff.thunk (fun () ->
          events.Add $"next {x}"
          x + 1))
      |> Helpers.run

    equal value (Exit.Ok 2)
    arrEqual (events.ToArray()) [| "after"; "cleanup"; "next 1" |]
  }

  Vitest.itp "defer cleanup failure stops outer continuation chained after the computation expression" <| fun () -> await {
    let events = ResizeArray<string>()

    let! value =
      (eff {
        let! x = Pure 1
        defer (Err "cleanup failed")
        do! Eff.thunk (fun () -> events.Add $"after {x}")
        return x
      })
      |> Eff.bind (fun x ->
        Eff.thunk (fun () ->
          events.Add $"next {x}"
          x + 1))
      |> Helpers.run

    equal value (Exit.Err "cleanup failed")
    arrEqual (events.ToArray()) [| "after 1" |]
  }

  Vitest.itp "multiple defers still run in LIFO order on failure" <| fun () -> await {
    let events = ResizeArray<string>()

    let! value =
      eff {
        defer (Eff.thunk (fun () -> events.Add "outer"))
        defer (Eff.thunk (fun () -> events.Add "inner"))
        return! Err "boom"
      }
      |> Helpers.run

    equal value (Exit.Err "boom")
    arrEqual (events.ToArray()) [| "inner"; "outer" |]
  }
