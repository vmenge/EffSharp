namespace Gen.Fixtures.UnsupportedMemberKind

open EffFs.Gen

[<Effect>]
type IThing =
  abstract Name: string
  abstract Run: int -> unit
