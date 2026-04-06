namespace ConditionalDiscoveryRed

open EffFs.Gen

#if EFFECTGEN_DISCOVERY
[<Effect>]
type IGreeter =
  abstract Greet: string -> string
#endif
