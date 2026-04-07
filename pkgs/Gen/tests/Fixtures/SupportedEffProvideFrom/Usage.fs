namespace SupportedEffProvideFromRed

open EffSharp.Core

module Usage =
  let spawnProgram () : Eff<JobHandle<JobResult>, SpawnError, #Effect.RuntimeService> =
    IRuntimeService.Spawn { Id = 7 }
