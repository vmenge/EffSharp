namespace SupportedEffExactRed

open EffFs.Core
open EffFs.EffectGen

[<Effect>]
type IRuntime =
  abstract Spawn: Job -> Eff<JobHandle<JobResult>, SpawnError, unit>
