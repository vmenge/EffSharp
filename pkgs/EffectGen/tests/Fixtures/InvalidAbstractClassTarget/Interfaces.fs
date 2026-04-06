namespace EffectGen.Fixtures.InvalidAbstractClassTarget

open EffFs.EffectGen

[<AbstractClass>]
[<Effect>]
type BadService =
  abstract Fetch: string -> string
