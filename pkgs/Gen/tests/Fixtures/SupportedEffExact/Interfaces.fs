namespace SupportedEffExactRed

open EffFs.Core
open EffFs.Gen

[<Effect>]
type IRuntime =
  abstract Spawn: Job -> Eff<JobHandle<JobResult>, SpawnError, unit>
