namespace Gen.Fixtures.UnsupportedReturnShape

open EffFs.Core
open EffFs.Gen

type NeededEnv = { Value: int }

[<Effect>]
type IRunner =
  abstract spawn: int -> Eff<string, string, NeededEnv>
