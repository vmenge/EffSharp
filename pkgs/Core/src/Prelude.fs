namespace EffSharp.Core

[<AutoOpen>]
module internal Prelude =
#if FABLE_COMPILER
  open Fable.Core

  type Await<'t> = JS.Promise<'t>
  let await = promise

  module Await =
    let from = Promise.lift

    let waitAsync (token: System.Threading.CancellationToken) (a: Await<'t>) =
      let cancelPromise =
        Promise.create (fun _resolve reject ->
          token.Register(fun () ->
            reject (System.OperationCanceledException() :> exn)
          )
          |> ignore
        )

      Promise.race ([| a; cancelPromise |])

    let onCompleted (f: 't -> unit) (a: Await<'t>) : unit =
      a |> Promise.map f |> ignore

    let run (f: unit -> Await<'t>) : Await<'t> = f ()

    let isCompleted (_a: Await<'t>) : bool = false

    let delay (duration: System.TimeSpan) : Await<unit> =
      Promise.create (fun resolve _ ->
        Fable.Core.JS.setTimeout
          (fun () -> resolve ())
          (int duration.TotalMilliseconds)
        |> ignore
      )

    let whenAny (tasks: Await<obj>[]) : Await<obj> = Promise.race tasks

    let ofAsync (a: Async<'t>) : Await<'t> = Async.StartAsPromise a

    let map f p = promise {
      let! p = p
      return f p
    }


  type Queue<'t>() =
    let items = ResizeArray<'t>()

    member _.Enqueue(value: 't) = items.Add(value)

    member _.TryDequeue(result: 't byref) =
      if items.Count > 0 then
        result <- items.[0]
        items.RemoveAt(0)
        true
      else
        false

    member _.IsEmpty = items.Count = 0

  type Sem(initialCount: int) =
    let mutable count = initialCount
    let mutable waiters = ResizeArray<(unit -> unit)>()

    member _.Release() =
      if waiters.Count > 0 then
        let resolve = waiters.[0]
        waiters.RemoveAt(0)
        resolve ()
      else
        count <- count + 1

      0 // match SemaphoreSlim.Release() return

    member _.WaitAsync() : Await<unit> =
      if count > 0 then
        count <- count - 1
        Promise.lift ()
      else
        Promise.create (fun resolve _reject -> waiters.Add(resolve))

    member _.Dispose() = waiters.Clear()

  type CDict<'k, 'v when 'k: equality>() =
    let dict = System.Collections.Generic.Dictionary<'k, 'v>()

    member _.IsEmpty = dict.Count = 0

    member _.TryAdd(key: 'k, value: 'v) =
      if dict.ContainsKey key then false
      else dict.[key] <- value; true

    member _.TryRemove(key: 'k) =
      dict.Remove(key)

    member _.Values = dict.Values :> seq<'v>

  type IdGen() =
    let mutable id = 0L
    member _.Next() = id <- id + 1L; id

  module CancellationToken =
    let canBeCanceled (_token: System.Threading.CancellationToken) = true
#else
  open System
  open System.Threading.Tasks

  type Await<'t> = Task<'t>

  module Await =
    let from = Task.FromResult

    let waitAsync (token: System.Threading.CancellationToken) (a: Await<'t>) =
      a.WaitAsync token

    let onCompleted (f: 't -> unit) (a: Await<'t>) : unit =
      a.ContinueWith(
        System.Action<Task<'t>>(fun completed -> f completed.Result),
        TaskContinuationOptions.ExecuteSynchronously
      )
      |> ignore

    let run (f: unit -> Await<'t>) : Await<'t> =
      let func: Func<Task<'t>> = Func<Task<'t>>(fun () -> f ())
      Task.Run<'t> func

    let isCompleted (a: Await<'t>) : bool = a.IsCompleted

    let delay (duration: TimeSpan) : Await<unit> = task {
      do! Task.Delay duration
    }

    let whenAny (tasks: Await<obj> array) : Await<obj> = task {
      let! result = Task.WhenAny tasks
      return! result
    }

    let ofAsync (a: Async<'t>) : Await<'t> = Async.StartAsTask a

    let map f t = task {
      let! t = t
      return f t
    }

  let await = task
  type Queue<'t> = System.Collections.Concurrent.ConcurrentQueue<'t>
  type Sem = System.Threading.SemaphoreSlim
  type CDict<'k, 'v when 'k: equality> = System.Collections.Concurrent.ConcurrentDictionary<'k, 'v>

  [<Struct>]
  type IdGen =
    val mutable private id: int64
    member this.Next() = System.Threading.Interlocked.Increment(&this.id)

  module CancellationToken =
    let canBeCanceled (token: System.Threading.CancellationToken) = token.CanBeCanceled
#endif
