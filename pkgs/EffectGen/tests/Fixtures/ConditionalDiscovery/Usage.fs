namespace ConditionalDiscoveryRed

open EffFs.Core

module Usage =
  let greetProgram () : Eff<string, exn, #EGreeter> = EGreeter.greet "Ada"
