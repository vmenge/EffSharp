namespace SupportedSyncRed

open EffSharp.Core

module Usage =
  let logProgram () : Eff<unit, exn, #ILogger> = ILogger.Debug "hello"
  let clockProgram () : Eff<string, exn, #IClock> = IClock.Now ()
  let parserProgram () : Eff<int, ParseError, #IParser> = IParser.Parse "42"
  let lookupProgram () : Eff<User, LookupError, #ILookup> = ILookup.TryFind (1, "user")
