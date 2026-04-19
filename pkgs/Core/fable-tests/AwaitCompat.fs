namespace EffSharp.Core.FableTests

[<AutoOpen>]
module AwaitCompat =
#if FABLE_COMPILER
  open System
  open Fable.Core

  type Await<'t> = JS.Promise<'t>
  let await = promise

  module Await =
    let from = Promise.lift

    let delay (duration: TimeSpan) : Await<unit> =
      Promise.create (fun resolve _ ->
        JS.setTimeout
          (fun () -> resolve ())
          (int duration.TotalMilliseconds)
        |> ignore
      )
#else
  open System
  open System.Threading.Tasks

  type Await<'t> = Task<'t>
  let await = task

  module Await =
    let from = Task.FromResult

    let delay (duration: TimeSpan) : Await<unit> = task {
      do! Task.Delay duration
    }
#endif
