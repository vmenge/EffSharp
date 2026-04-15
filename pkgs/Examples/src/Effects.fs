namespace EffSharp.Examples

open EffSharp.Core
open EffSharp.Gen
open System

[<Effect>]
type Log =
  abstract info: string -> unit

[<Effect>]
type Clock =
  abstract now: unit -> DateTime
