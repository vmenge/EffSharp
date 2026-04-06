namespace EffSharp.Examples

open EffSharp.Core
open EffSharp.Gen
open System

[<Effect>]
type ILogger =
  abstract info: string -> unit

[<Effect>]
type IClock =
  abstract now: unit -> DateTime

[<Effect>]
type IFs =
  abstract readToString: string -> Eff<string, string, unit>
