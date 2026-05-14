namespace EffSharp.AspNetCore.Tests

open Expecto

module Program =
  [<EntryPoint>]
  let main argv =
    runTestsWithCLIArgs [] argv (testList "all" [ Http.tests ])
