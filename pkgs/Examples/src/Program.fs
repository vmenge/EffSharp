module EffSharp.Examples.Program

open EffSharp.Core
open EffSharp.Std
open System

type AppEnv() =
  interface Log with
    member _.info msg = printfn "%s" msg

  interface Clock with
    member _.now() = DateTime.Now

  interface Effect.Fs with
    member _.Fs = Fs.Provider()

let program () = eff {
  let! now = Clock.now ()
  do! Log.info $"starting program at {now}"

  let! contents = Path.make "./test" |> Fs.readText
  do! Log.info $"file contents: {contents}"

  return ()
}

[<EntryPoint>]
let main _ =
  program () |> Eff.runSync (AppEnv()) |> ignore

  0
