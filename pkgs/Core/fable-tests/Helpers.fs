namespace EffSharp.Core.FableTests

open EffSharp.Core
#if FABLE_COMPILER
open Fable.Core
#else
open System.Threading.Tasks
#endif

type Gate<'t>() =
#if FABLE_COMPILER
  let mutable resolved = false
  let mutable value = Unchecked.defaultof<'t>
  let waiters = ResizeArray<'t -> unit>()

  member _.Await : Await<'t> =
    if resolved then
      Await.from value
    else
      Promise.create (fun resolve _ -> waiters.Add resolve)

  member _.TrySetResult(result: 't) =
    if resolved then
      false
    else
      resolved <- true
      value <- result

      for resolve in waiters do
        resolve result

      waiters.Clear()
      true
#else
  let tcs = TaskCompletionSource<'t>()

  member _.Await : Await<'t> = tcs.Task
  member _.TrySetResult(result: 't) = tcs.TrySetResult result
#endif

module Helpers =
  let run eff : Await<_> = Eff.run () eff

  let gate<'t> () = Gate<'t>()
