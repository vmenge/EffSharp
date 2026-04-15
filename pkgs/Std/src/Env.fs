namespace EffSharp.Std

open EffSharp.Gen
open System.Collections

[<Effect(Mode.Wrap)>]
type Env =
  abstract get: string -> string option
  abstract getAll: unit -> (string * string) array
  abstract args: unit -> string array

type EnvProvider internal () =
  interface Env with
    member _.args() = System.Environment.GetCommandLineArgs()

    member _.get(arg1: string) =
      System.Environment.GetEnvironmentVariable(arg1) |> Option.ofObj

    member _.getAll() =
      System.Environment.GetEnvironmentVariables()
      |> Seq.cast<DictionaryEntry>
      |> Seq.map (fun e -> string e.Key, string e.Value)
      |> Seq.toArray

[<AutoOpen>]
module EnvExt =
  type Env with
    static member Provider() = EnvProvider()
