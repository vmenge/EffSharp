namespace EntryPointOrdering

open EffSharp.Gen

[<Effect>]
type IGreeter =
  abstract greet: string -> string
