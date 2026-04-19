module EffSharp.Core.FableTests.ConcurrencyTests

open System
open EffSharp.Core
open EffSharp.Core.FableTests
open EffSharp.Core.FableTests.Expect

Vitest.describe "concurrency under Fable" <| fun () ->
  Vitest.itp "abort waits for child ensure cleanup before returning" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let child =
      eff {
        do! Eff.thunk (fun () -> events.Add "child-start")
        do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
        let! value = never.Await
        return value
      }
      |> Eff.ensure (
        eff {
          do! Eff.thunk (fun () -> events.Add "child-cleanup-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "child-cleanup-end")
        }
      )

    let releaseCleanup = await {
      let! _ = cleanupStarted.Await
      events.Add "cleanup-observed"
      cleanupRelease.TrySetResult(()) |> ignore
    }

    let! result =
      eff {
        let! fiber = Eff.fork child
        do! entered.Await
        let releaseTask = releaseCleanup
        do! Fiber.abort fiber
        do! releaseTask
      }
      |> Helpers.run

    events.Add "after-abort"

    equal result (Exit.Ok())
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "child-cleanup-start"
        "cleanup-observed"
        "child-cleanup-end"
        "after-abort"
      |]
  }

  Vitest.itp "abort waits for child defer cleanup before returning" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let child = eff {
      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "child-defer-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "child-defer-end")
        }
      )

      do! Eff.thunk (fun () -> events.Add "child-start")
      do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
      let! value = never.Await
      return value
    }

    let releaseCleanup = await {
      let! _ = cleanupStarted.Await
      events.Add "defer-observed"
      cleanupRelease.TrySetResult(()) |> ignore
    }

    let! result =
      eff {
        let! fiber = Eff.fork child
        do! entered.Await
        let releaseTask = releaseCleanup
        do! Fiber.abort fiber
        do! releaseTask
      }
      |> Helpers.run

    events.Add "after-abort"

    equal result (Exit.Ok())
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "child-defer-start"
        "defer-observed"
        "child-defer-end"
        "after-abort"
      |]
  }

  Vitest.itp "abort ensure cleanup failure overrides Abort" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let child =
      eff {
        do! Eff.thunk (fun () -> events.Add "child-start")
        do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
        let! value = never.Await
        return value
      }
      |> Eff.ensure (
        eff {
          do! Eff.thunk (fun () -> events.Add "child-cleanup-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "child-cleanup-fail")
          return! Err "cleanup failed"
        }
      )

    let releaseCleanup = await {
      let! _ = cleanupStarted.Await
      events.Add "cleanup-observed"
      cleanupRelease.TrySetResult(()) |> ignore
    }

    let! result =
      eff {
        let! fiber = Eff.fork child
        do! entered.Await
        let releaseTask = releaseCleanup
        do! Fiber.abort fiber
        do! releaseTask
      }
      |> Helpers.run

    events.Add "after-abort"

    equal result (Exit.Err "cleanup failed")
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "child-cleanup-start"
        "cleanup-observed"
        "child-cleanup-fail"
        "after-abort"
      |]
  }

  Vitest.itp "abort unwinds nested child defers in LIFO order" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let child = eff {
      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "outer-defer-start")
          do! Eff.thunk (fun () -> events.Add "outer-defer-end")
        }
      )

      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "inner-defer-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "inner-defer-end")
        }
      )

      do! Eff.thunk (fun () -> events.Add "child-start")
      do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
      let! value = never.Await
      return value
    }

    let releaseCleanup = await {
      let! _ = cleanupStarted.Await
      events.Add "defer-observed"
      cleanupRelease.TrySetResult(()) |> ignore
    }

    let! result =
      eff {
        let! fiber = Eff.fork child
        do! entered.Await
        let releaseTask = releaseCleanup
        do! Fiber.abort fiber
        do! releaseTask
      }
      |> Helpers.run

    events.Add "after-abort"

    equal result (Exit.Ok())
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "inner-defer-start"
        "defer-observed"
        "inner-defer-end"
        "outer-defer-start"
        "outer-defer-end"
        "after-abort"
      |]
  }

  Vitest.itp "abort inner child defer failure still runs outer defer" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let child = eff {
      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "outer-defer-start")
          do! Eff.thunk (fun () -> events.Add "outer-defer-end")
        }
      )

      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "inner-defer-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "inner-defer-fail")
          return! Err "inner cleanup failed"
        }
      )

      do! Eff.thunk (fun () -> events.Add "child-start")
      do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
      let! value = never.Await
      return value
    }

    let releaseCleanup = await {
      let! _ = cleanupStarted.Await
      events.Add "defer-observed"
      cleanupRelease.TrySetResult(()) |> ignore
    }

    let! result =
      eff {
        let! fiber = Eff.fork child
        do! entered.Await
        let releaseTask = releaseCleanup
        do! Fiber.abort fiber
        do! releaseTask
      }
      |> Helpers.run

    events.Add "after-abort"

    equal result (Exit.Err "inner cleanup failed")
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "inner-defer-start"
        "defer-observed"
        "inner-defer-fail"
        "outer-defer-start"
        "outer-defer-end"
        "after-abort"
      |]
  }

  Vitest.itp "abort outer child defer failure overrides inner defer failure" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let child = eff {
      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "outer-defer-start")
          do! Eff.thunk (fun () -> events.Add "outer-defer-fail")
          return! Err "outer cleanup failed"
        }
      )

      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "inner-defer-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "inner-defer-fail")
          return! Err "inner cleanup failed"
        }
      )

      do! Eff.thunk (fun () -> events.Add "child-start")
      do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
      let! value = never.Await
      return value
    }

    let releaseCleanup = await {
      let! _ = cleanupStarted.Await
      events.Add "defer-observed"
      cleanupRelease.TrySetResult(()) |> ignore
    }

    let! result =
      eff {
        let! fiber = Eff.fork child
        do! entered.Await
        let releaseTask = releaseCleanup
        do! Fiber.abort fiber
        do! releaseTask
      }
      |> Helpers.run

    events.Add "after-abort"

    equal result (Exit.Err "outer cleanup failed")
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "inner-defer-start"
        "defer-observed"
        "inner-defer-fail"
        "outer-defer-start"
        "outer-defer-fail"
        "after-abort"
      |]
  }

  Vitest.itp "timeout returns TimedOut when child cleanup succeeds" <| fun () -> await {
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let mutable cleaned = false

    let child =
      eff {
        let! value = never.Await
        return value
      }
      |> Eff.ensure (
        eff {
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> cleaned <- true)
        }
      )

    let timeoutTask =
      Eff.timeout (TimeSpan.FromMilliseconds 10.0) child
      |> Helpers.run

    let! _ = cleanupStarted.Await
    cleanupRelease.TrySetResult(()) |> ignore

    let! result = timeoutTask

    equal result (Exit.Ok(TimedOut: TimeoutResult<int>))
    isTrue cleaned
  }

  Vitest.itp "timeout cleanup failure overrides TimedOut" <| fun () -> await {
    let never = Helpers.gate<int> ()

    let! result =
      Eff.timeout
        (TimeSpan.FromMilliseconds 10.0)
        (
          eff {
            let! value = never.Await
            return value
          }
          |> Eff.ensure (Err "cleanup failed")
        )
      |> Helpers.run

    equal result (Exit.Err "cleanup failed")
  }

  Vitest.itp "timeout waits for child defer cleanup before returning TimedOut" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let child = eff {
      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "child-defer-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "child-defer-end")
        }
      )

      do! Eff.thunk (fun () -> events.Add "child-start")
      do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
      let! value = never.Await
      return value
    }

    let timeoutTask =
      Eff.timeout (TimeSpan.FromMilliseconds 10.0) child
      |> Helpers.run

    let! _ = entered.Await
    let! _ = cleanupStarted.Await
    events.Add "defer-observed"
    cleanupRelease.TrySetResult(()) |> ignore

    let! result = timeoutTask
    events.Add "after-timeout"

    equal result (Exit.Ok(TimedOut: TimeoutResult<int>))
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "child-defer-start"
        "defer-observed"
        "child-defer-end"
        "after-timeout"
      |]
  }

  Vitest.itp "timeout defer cleanup failure overrides TimedOut" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let child = eff {
      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "child-defer-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "child-defer-fail")
          return! Err "cleanup failed"
        }
      )

      do! Eff.thunk (fun () -> events.Add "child-start")
      do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
      let! value = never.Await
      return value
    }

    let timeoutTask =
      Eff.timeout (TimeSpan.FromMilliseconds 10.0) child
      |> Helpers.run

    let! _ = entered.Await
    let! _ = cleanupStarted.Await
    events.Add "defer-observed"
    cleanupRelease.TrySetResult(()) |> ignore

    let! result = timeoutTask
    events.Add "after-timeout"

    equal result (Exit.Err "cleanup failed")
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "child-defer-start"
        "defer-observed"
        "child-defer-fail"
        "after-timeout"
      |]
  }

  Vitest.itp "race waits for loser cleanup before returning winner" <| fun () -> await {
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let mutable cleaned = false

    let loser =
      eff {
        let! value = never.Await
        return value
      }
      |> Eff.ensure (
        eff {
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> cleaned <- true)
        }
      )

    let raceTask = Eff.race (Pure 1) loser |> Helpers.run

    let! _ = cleanupStarted.Await
    cleanupRelease.TrySetResult(()) |> ignore

    let! result = raceTask

    equal result (Exit.Ok 1)
    isTrue cleaned
  }

  Vitest.itp "race waits for loser defer cleanup before returning winner" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let winnerRelease = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let winner = eff {
      do! winnerRelease.Await
      return 1
    }

    let loser = eff {
      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "child-defer-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "child-defer-end")
        }
      )

      do! Eff.thunk (fun () -> events.Add "child-start")
      do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
      let! value = never.Await
      return value
    }

    let raceTask = Eff.race winner loser |> Helpers.run

    let! _ = entered.Await
    winnerRelease.TrySetResult(()) |> ignore
    let! _ = cleanupStarted.Await
    events.Add "defer-observed"
    cleanupRelease.TrySetResult(()) |> ignore

    let! result = raceTask
    events.Add "after-race"

    equal result (Exit.Ok 1)
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "child-defer-start"
        "defer-observed"
        "child-defer-end"
        "after-race"
      |]
  }

  Vitest.itp "race loser defer cleanup failure overrides winner" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let winnerRelease = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let winner = eff {
      do! winnerRelease.Await
      return 1
    }

    let loser = eff {
      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "child-defer-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "child-defer-fail")
          return! Err "cleanup failed"
        }
      )

      do! Eff.thunk (fun () -> events.Add "child-start")
      do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
      let! value = never.Await
      return value
    }

    let raceTask = Eff.race winner loser |> Helpers.run

    let! _ = entered.Await
    winnerRelease.TrySetResult(()) |> ignore
    let! _ = cleanupStarted.Await
    events.Add "defer-observed"
    cleanupRelease.TrySetResult(()) |> ignore

    let! result = raceTask
    events.Add "after-race"

    equal result (Exit.Err "cleanup failed")
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "child-defer-start"
        "defer-observed"
        "child-defer-fail"
        "after-race"
      |]
  }

  Vitest.itp "race returns the first completed error" <| fun () -> await {
    let slowSuccess = eff {
      do! Await.delay (TimeSpan.FromMilliseconds 25.0)
      return 1
    }

    let! result = Eff.race (Err "boom") slowSuccess |> Helpers.run

    equal result (Exit.Err "boom")
  }

  Vitest.itp "all aborts siblings on error and waits for cleanup" <| fun () -> await {
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let mutable cleaned = false

    let blocked =
      eff {
        let! value = never.Await
        return value
      }
      |> Eff.ensure (
        eff {
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> cleaned <- true)
        }
      )

    let allTask = Eff.all [ blocked; Err "boom" ] |> Helpers.run

    let! _ = cleanupStarted.Await
    cleanupRelease.TrySetResult(()) |> ignore

    let! result = allTask

    equal result (Exit.Err "boom")
    isTrue cleaned
  }

  Vitest.itp "all aborts siblings with defer cleanup and waits before returning" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let blocked = eff {
      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "child-defer-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "child-defer-end")
        }
      )

      do! Eff.thunk (fun () -> events.Add "child-start")
      do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
      let! value = never.Await
      return value
    }

    let firstFailure = eff {
      do! entered.Await
      return! Err "boom"
    }

    let allTask = Eff.all [ blocked; firstFailure ] |> Helpers.run

    let! _ = cleanupStarted.Await
    events.Add "defer-observed"
    cleanupRelease.TrySetResult(()) |> ignore

    let! result = allTask
    events.Add "after-all"

    equal result (Exit.Err "boom")
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "child-defer-start"
        "defer-observed"
        "child-defer-end"
        "after-all"
      |]
  }

  Vitest.itp "all aborted sibling defer cleanup failure overrides first terminal exit" <| fun () -> await {
    let entered = Helpers.gate<unit> ()
    let cleanupStarted = Helpers.gate<unit> ()
    let cleanupRelease = Helpers.gate<unit> ()
    let never = Helpers.gate<int> ()
    let events = ResizeArray<string>()

    let blocked = eff {
      defer (
        eff {
          do! Eff.thunk (fun () -> events.Add "child-defer-start")
          do! Eff.thunk (fun () -> cleanupStarted.TrySetResult(()) |> ignore)
          do! cleanupRelease.Await
          do! Eff.thunk (fun () -> events.Add "child-defer-fail")
          return! Err "cleanup failed"
        }
      )

      do! Eff.thunk (fun () -> events.Add "child-start")
      do! Eff.thunk (fun () -> entered.TrySetResult(()) |> ignore)
      let! value = never.Await
      return value
    }

    let firstFailure = eff {
      do! entered.Await
      return! Err "boom"
    }

    let allTask = Eff.all [ blocked; firstFailure ] |> Helpers.run

    let! _ = cleanupStarted.Await
    events.Add "defer-observed"
    cleanupRelease.TrySetResult(()) |> ignore

    let! result = allTask
    events.Add "after-all"

    equal result (Exit.Err "cleanup failed")
    arrEqual
      (events.ToArray())
      [|
        "child-start"
        "child-defer-start"
        "defer-observed"
        "child-defer-fail"
        "after-all"
      |]
  }
