namespace EffSharp.Gen.Tests

open System.IO
open Expecto

module RegressionE2E =
  open Harness

  let private fixtureDirectory fixtureName =
    Path.Combine(__SOURCE_DIRECTORY__, "Fixtures", fixtureName)

  let private fixtureProject fixtureName =
    Path.Combine(fixtureDirectory fixtureName, $"{fixtureName}.fsproj")

  let private intermediateDirectory fixtureName =
    Path.Combine(fixtureDirectory fixtureName, "obj", "Debug", "net10.0")

  let private generatedDirectory fixtureName =
    Path.Combine(intermediateDirectory fixtureName, "Gen")

  let private cleanupIntermediateDirectory fixtureName =
    try
      let path = intermediateDirectory fixtureName

      if Directory.Exists(path) then
        Directory.Delete(path, true)
    with :? DirectoryNotFoundException ->
      ()

  let tests =
    testSequenced <| testList "RegressionE2E" [
      testTask "generated files carry source open namespaces needed for unqualified types" {
        let fixtureName = "ImportedTypeOpens"
        cleanupIntermediateDirectory fixtureName

        let! result = buildProject (fixtureProject fixtureName)

        Expect.equal result.ExitCode 0 $"fixture {fixtureName} should build successfully when generated files preserve imported namespaces. Output:{System.Environment.NewLine}{result.Output}"

        let generatedText =
          Path.Combine(generatedDirectory fixtureName, "EClock.g.fs")
          |> File.ReadAllText

        Expect.stringContains generatedText "open System" "generated files should carry forward imported namespaces from the declaring source file"
        Expect.stringContains generatedText "let now () : Eff<DateTime, 'e, #EClock>" "generated files should keep unqualified imported type names valid"
      }

      testTask "source-mode compile ordering keeps the entry-point file last" {
        let fixtureName = "EntryPointOrdering"
        cleanupIntermediateDirectory fixtureName

        let! result = buildProject (fixtureProject fixtureName)

        Expect.equal result.ExitCode 0 $"fixture {fixtureName} should build successfully when source-mode compile ordering preserves the last user file. Output:{System.Environment.NewLine}{result.Output}"

        let orderedCompileItems =
          Path.Combine(generatedDirectory fixtureName, "ordered-compile-items.txt")
          |> File.ReadAllLines

        let expectedLastFile =
          Path.Combine(fixtureDirectory fixtureName, "Program.fs")

        Expect.equal orderedCompileItems[orderedCompileItems.Length - 1] expectedLastFile "the entry-point file should remain the last file in the rewritten compile order"
      }
    ]
