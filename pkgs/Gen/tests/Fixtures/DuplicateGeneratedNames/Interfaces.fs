namespace Gen.Fixtures.DuplicateGeneratedNames

open EffFs.Gen

[<Effect>]
type ILogger =
  abstract Log: string -> unit

[<Effect>]
type Logger =
  abstract Write: string -> unit
