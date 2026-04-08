namespace EffSharp.Std

[<AutoOpen>]
module Utils =
  let tap f x =
    f x |> ignore
    x
