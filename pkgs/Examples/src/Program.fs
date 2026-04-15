module EffSharp.Examples.Program

open EffSharp.Core
open EffSharp.Std
open System
open type EffSharp.Std.Console

type AppEnv() =
  interface Effect.Console with
    member _.Console = Console.Provider()

  interface Effect.Fs with
    member _.Fs = Fs.Provider()

  interface Effect.Clock with
    member _.Clock = Clock.Provider()

  interface Effect.Env with
    member _.Env = Env.Provider()

let program () = eff {
  let! now = Clock.now ()
  do! println $"starting program at {now}"

  let! myvar = Env.get "MYVAR"
  do! println $"MYVAR: {myvar}"

  return ()
}

[<EntryPoint>]
let main _ =
  program () |> Eff.runSync (AppEnv()) |> printfn "%O"

  0
