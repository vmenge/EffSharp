namespace SupportedEffProvideFromRed

open EffFs.Core
open EffFs.EffectGen

type IRuntimeEnv =
  abstract RuntimeService: IRuntimeService

and [<Effect>] IRuntimeService =
  abstract Spawn: Job -> Eff<JobHandle<JobResult>, SpawnError, IRuntimeEnv>
