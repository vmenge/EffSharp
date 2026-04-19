namespace EffSharp.Core

open System.Threading.Tasks

[<AutoOpen>]
module ReportCE =
  type EffrBuilder() =
    inherit CE.EffBuilderBase()

    member _.ReturnFrom(eff: Eff<'t, 'e, 'env>) : Eff<'t, exn, 'env> =
      eff |> Eff.mapErr Report.make

    member _.Bind
      (eff: Eff<'t, 'e, 'env>, f: 't -> Eff<'u, 'e, 'env>)
      : Eff<'u, exn, 'env> =
      Eff.bind f eff |> Eff.mapErr Report.make

    member _.BindReturn
      (eff: Eff<'t, 'e, 'env>, f: 't -> 'u)
      : Eff<'u, exn, 'env> =
      Eff.map f eff |> Eff.mapErr Report.make

    member _.Source(eff: Eff<'t, 'e, 'env>) : Eff<'t, exn, 'env> =
      eff |> Eff.mapErr Report.make

  [<AutoOpen>]
  module CEExtLowPriority =
    type EffrBuilder with
#if !FABLE_COMPILER
      member _.Source(valueTask: ValueTask<'t>) : Eff<'t, 'e, 'env> =
        Eff.ofValueTask (fun () -> valueTask)

      member _.Source(task: Await<'t>) : Eff<'t, 'e, 'env> =
        Eff.ofTask (fun () -> task)
#else
      member _.Source(task: Await<'t>) : Eff<'t, 'e, 'env> =
        Eff.ofPromise (fun () -> task)
#endif
      member _.Source(async: Async<'t>) : Eff<'t, 'e, 'env> =
        Eff.ofAsync (fun () -> async)

  [<AutoOpen>]
  module CEExtHighPriority =
    type EffrBuilder with

#if !FABLE_COMPILER
      member _.Source
        (valueTaskResult: ValueTask<Result<'t, 'e>>)
        : Eff<'t, exn, 'env> =
        Eff.ofValueTask (fun () -> valueTaskResult)
        |> Eff.bind (Eff.ofResult >> (Eff.mapErr Report.make))

      member _.Source(taskResult: Await<Result<'t, 'e>>) : Eff<'t, exn, 'env> =
        Eff.ofTask (fun () -> taskResult)
        |> Eff.bind Eff.ofResult
        |> Eff.mapErr Report.make
#else
      member _.Source(taskResult: Await<Result<'t, 'e>>) : Eff<'t, exn, 'env> =
        Eff.ofPromise (fun () -> taskResult)
        |> Eff.bind Eff.ofResult
        |> Eff.mapErr Report.make
#endif
      member _.Source(asyncResult: Async<Result<'t, 'e>>) : Eff<'t, exn, 'env> =
        Eff.ofAsync (fun () -> asyncResult)
        |> Eff.bind Eff.ofResult
        |> Eff.mapErr Report.make

      member _.Source(result: Result<'t, 'e>) : Eff<'t, exn, 'env> =
        Eff.ofResult result |> Eff.mapErr Report.make

      member _.Source(option: Option<'t>) : Eff<'t, exn, 'env> =
        Eff.ofOption option

      member _.Source(valueOption: ValueOption<'t>) : Eff<'t, exn, 'env> =
        Eff.ofValueOption valueOption

  let effr = EffrBuilder()
