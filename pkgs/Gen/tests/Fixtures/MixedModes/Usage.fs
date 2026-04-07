namespace MixedModesRed

open EffSharp.Core

module Usage =
  let logProgram () : Eff<unit, exn, #ILogger> = ILogger.Info "hello"
  let clockProgram () : Eff<string, exn, #Effect.Clock> = IClock.Now ()
