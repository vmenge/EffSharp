module EffSharp.Examples.Program

open EffSharp.Core
open EffSharp.Std
open System
open type EffSharp.Std.Stdio
open System.Text

let whatever () = effr {
  let! random = Random.intRange 10 20
  return random + 50
}

let program (z: int) = effr {
  let! a = whatever ()
  let x = 5
  let y = 10
  let sum = x + y + z
  let! date = Clock.utcNow ()
  do! Stdio.println $"sum is {x + y}"

  return x + y
}

[<EntryPoint>]
let main _ =
  program 10 |> Eff.runSync (Std.Provider()) |> printfn "%O"

  0
