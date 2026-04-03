namespace Core

open System.Threading.Tasks

[<Struct>]
type Eff<'t, 'env> =
    private
    | Pure of value: 't
    | Err of err: exn
    | Delay of delay: (unit -> Eff<'t, 'env>)
    | Thunk of thunk: (unit -> 't)
    | Tsk of tsk: (unit -> Task<'t>)
    | Asy of asy: (unit -> Async<'t>)
    | Pending of pending: obj * step: Step<'t, 'env>

and private Step<'t, 'env> = delegate of obj * 'env -> ValueTask<Eff<'t, 'env>>

exception ValueIsNone

module Eff =
    let value t = Pure t
    let err e = Err e
    let errwith msg = Err(exn msg)
    let delay f = Delay f
    let thunk f = Thunk f

    let ofResult (r: Result<'t, #exn>) =
        match r with
        | Ok v -> Pure v
        | Error e -> Err e

    let ofResultWith r f =
        match r with
        | Ok v -> Pure v
        | Error e -> Err(f e)

    let ofOption o =
        match o with
        | Some v -> Pure v
        | None -> Err(ValueIsNone)

    let ofOptionWith o f =
        match o with
        | Some v -> Pure v
        | None -> Err(f ())

    let ofValueOption o =
        match o with
        | ValueSome v -> Pure v
        | ValueNone -> Err(ValueIsNone)

    let ofValueOptionWith o f =
        match o with
        | ValueSome v -> Pure v
        | ValueNone -> Err(f ())

    let ofTask f = Tsk f

    let ofValueTask f =
        Tsk(fun () ->
            task {
                let! x = f ()
                return x
            })

    let ofAsync async = Asy async

    type private MapPendingState<'t, 'u, 'env> =
        { Pending: obj
          Step: Step<'t, 'env>
          Map: 't -> 'u }

    let rec private mapPendingStep<'t, 'u, 'env> () =
        Step<'u, 'env>(fun stateObj env ->
            let state: MapPendingState<'t, 'u, 'env> = unbox stateObj

            let run =
                task {
                    try
                        let! next = state.Step.Invoke(state.Pending, env).AsTask()
                        return map next state.Map
                    with e ->
                        return Err e
                }

            ValueTask<Eff<'u, 'env>>(run))

    and map ef f =
        match ef with
        | Pure v -> Pure(f v)
        | Err err -> Err err
        | Delay delay -> Delay(fun () -> map (delay ()) f)
        | Thunk thunk -> Thunk(fun () -> f (thunk ()))
        | Tsk t ->
            Tsk(fun () ->
                task {
                    let! x = t ()
                    return f x
                })
        | Asy asy ->
            Asy(fun () ->
                async {
                    let! x = asy ()
                    return f x
                })

        | Pending(pending, step) ->
            let pending =
                box
                    { Pending = pending
                      Step = step
                      Map = f }

            let step = mapPendingStep ()

            Pending(pending, step)

    type private BindTaskState<'t, 'u, 'env> =
        { Source: unit -> Task<'t>
          Continue: 't -> Eff<'u, 'env> }

    let rec private bindTaskStep<'t, 'u, 'env> () =
        Step<'u, 'env>(fun stateObj _env ->
            let state: BindTaskState<'t, 'u, 'env> = unbox stateObj

            let run =
                task {
                    try
                        let! x = state.Source()
                        return state.Continue x
                    with e ->
                        return Err e
                }

            ValueTask<Eff<'u, 'env>>(run))

    type private BindAsyncState<'t, 'u, 'env> =
        { Source: unit -> Async<'t>
          Continue: 't -> Eff<'u, 'env> }

    let rec private bindAsyncStep<'t, 'u, 'env> () =
        Step<'u, 'env>(fun stateObj _env ->
            let state: BindAsyncState<'t, 'u, 'env> = unbox stateObj

            let run =
                task {
                    try
                        let! x = state.Source() |> Async.StartAsTask
                        return state.Continue x
                    with e ->
                        return Err e
                }

            ValueTask<Eff<'u, 'env>>(run))


    type private BindPendingState<'t, 'u, 'env> =
        { Pending: obj
          Step: Step<'t, 'env>
          Continue: 't -> Eff<'u, 'env> }

    let rec private bindPendingStep<'t, 'u, 'env> () =
        Step<'u, 'env>(fun stateObj env ->
            let state: BindPendingState<'t, 'u, 'env> = unbox stateObj

            let run =
                task {
                    try
                        let! next = state.Step.Invoke(state.Pending, env).AsTask()
                        return bind next state.Continue
                    with e ->
                        return Err e
                }

            ValueTask<Eff<'u, 'env>>(run))

    and bind ef f =
        match ef with
        | Pure v -> f v
        | Err err -> Err err
        | Delay delay -> Delay(fun () -> bind (delay ()) f)
        | Thunk thunk -> Delay(fun () -> f (thunk ()))
        | Tsk tsk ->
            let state: BindTaskState<'t, 'u, 'env> = { Source = tsk; Continue = f }
            let pending = box state
            let step = bindTaskStep ()

            Pending(pending, step)

        | Asy asy ->
            let state: BindAsyncState<'t, 'u, 'env> = { Source = asy; Continue = f }
            let pending = box state
            let step = bindAsyncStep ()

            Pending(pending, step)

        | Pending(pending, step) ->
            let state: BindPendingState<'t, 'u, 'env> =
                { Pending = pending
                  Step = step
                  Continue = f }

            let pending = box state
            let step = bindPendingStep ()

            Pending(pending, step)
