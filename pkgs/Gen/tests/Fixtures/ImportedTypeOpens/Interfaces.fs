namespace ImportedTypeOpens

open System
open EffSharp.Gen

[<Effect>]
type IClock =
  abstract now: unit -> DateTime
