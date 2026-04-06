module EffSharp.Examples.Program

open EffSharp.Core
open System

type AppEnv() =
  interface ELogger with
    member _.Logger =
      { new ILogger with
          member _.info msg = printfn "%s" msg
      }

  interface EClock with
    member _.Clock =
      { new IClock with
          member _.now() = DateTime.Now
      }

  interface EFs with
    member _.Fs =
      { new IFs with
          member _.readToString _path = Pure "contents"
      }


let program () = eff {
  let! now = EClock.now ()
  do! ELogger.info $"starting program at {now}"

  let! contents = EFs.readToString "filepath"
  do! ELogger.info $"file contents are {contents}"

  return ()
}

[<EntryPoint>]
let main _ =
  program () |> Eff.runSync (AppEnv()) |> ignore

  0
