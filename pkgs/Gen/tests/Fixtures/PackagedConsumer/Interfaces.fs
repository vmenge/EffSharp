namespace PackagedConsumer

open EffFs.Gen

[<Effect>]
type IGreeter =
  abstract Greet: string -> string
