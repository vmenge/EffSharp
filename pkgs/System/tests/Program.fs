namespace EffSharp.System.Tests

open Expecto

module Program =
  [<EntryPoint>]
  let main argv =
    runTestsWithCLIArgs
      []
      argv
      (testList "all" [ Stdio.tests; Path.tests; Cmd.tests ])
