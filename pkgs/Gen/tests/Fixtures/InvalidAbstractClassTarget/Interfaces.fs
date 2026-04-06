namespace Gen.Fixtures.InvalidAbstractClassTarget

open EffFs.Gen

[<AbstractClass>]
[<Effect>]
type BadService =
  abstract Fetch: string -> string
