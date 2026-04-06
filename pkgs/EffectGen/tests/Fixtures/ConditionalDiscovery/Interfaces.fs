namespace ConditionalDiscoveryRed

open EffFs.EffectGen

#if EFFECTGEN_DISCOVERY
[<Effect>]
type IGreeter =
  abstract Greet: string -> string
#endif
