module EntryPointOrdering.Program

open EffSharp.Core

type Greeter() =
  interface IGreeter with
    member _.greet(name: string) = $"Hello, {name}."

type AppEnv() =
  let greeter = Greeter() :> IGreeter

  interface EGreeter with
    member _.Greeter = greeter

let run () =
  match EGreeter.greet "ordering" |> Eff.runSync (AppEnv()) with
  | Exit.Ok "Hello, ordering." -> 0
  | Exit.Ok value -> failwithf "unexpected greeting %A" value
  | Exit.Err err -> failwithf "unexpected managed error %A" err
  | Exit.Exn ex -> raise ex

[<EntryPoint>]
let main _ =
  run ()
