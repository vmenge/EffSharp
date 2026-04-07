namespace DeletionRemovesGenerated

open EffSharp.Gen

[<Effect>]
type Clock =
  abstract now: unit -> string

[<Effect(Mode.Wrap)>]
type Logger =
  abstract log: string -> unit
