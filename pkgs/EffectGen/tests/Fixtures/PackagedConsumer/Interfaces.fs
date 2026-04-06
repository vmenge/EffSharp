namespace PackagedConsumer

open EffFs.EffectGen

[<Effect>]
type IGreeter =
  abstract Greet: string -> string
