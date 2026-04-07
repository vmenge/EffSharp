namespace WrappedNamingRed

open EffSharp.Core

module Usage =
  let greetProgram () : Eff<string, exn, #Effect.Greeter> = IGreeter.Greet "Ada"
  let logProgram () : Eff<unit, exn, #Effect.Logger> = Logger.Debug "hello"
  let oddLogProgram () : Eff<unit, exn, #Effect.Ilogger> = Ilogger.Trace "odd"
