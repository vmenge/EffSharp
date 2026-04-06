namespace EffFs.EffectGen.Tests

open System.IO
open Expecto

module SupportedEffExactE2E =
  open Harness

  let private fixtureName = "SupportedEffExact"

  let private fixtureDirectory =
    Path.Combine(__SOURCE_DIRECTORY__, "Fixtures", fixtureName)

  let private fixtureProject =
    Path.Combine(fixtureDirectory, $"{fixtureName}.fsproj")

  let private intermediateDirectory =
    Path.Combine(fixtureDirectory, "obj", "Debug", "net10.0")

  let private generatedDirectory =
    Path.Combine(intermediateDirectory, "EffectGen")

  let private cleanupIntermediateDirectory () =
    try
      if Directory.Exists(intermediateDirectory) then
        Directory.Delete(intermediateDirectory, true)
    with :? DirectoryNotFoundException ->
      ()

  let tests =
    testSequenced <| testList "SupportedEffExactE2E" [
      testTask "supported Eff exact fixture builds with generated wrappers in the same build" {
        cleanupIntermediateDirectory ()

        let! result = buildProject fixtureProject

        Expect.equal result.ExitCode 0 $"fixture {fixtureName} should build successfully once exact Eff generation exists. Output:{System.Environment.NewLine}{result.Output}"
      }

      testTask "supported Eff exact generated output flattens nested Eff returns" {
        cleanupIntermediateDirectory ()

        let! result = buildProject fixtureProject

        Expect.equal result.ExitCode 0 $"fixture {fixtureName} should build successfully before inspecting generated output. Output:{System.Environment.NewLine}{result.Output}"

        let generatedText =
          Directory.GetFiles(generatedDirectory, "*.g.fs")
          |> Array.sort
          |> Array.map File.ReadAllText
          |> String.concat System.Environment.NewLine

        Expect.stringContains generatedText "let spawn (arg1: Job) : Eff<JobHandle<JobResult>, SpawnError, #ERuntime>" "Eff-returning members should preserve the concrete success and error types"
        Expect.stringContains generatedText "|> Eff.flatten" "Exact Eff returns should flatten the nested Eff value"
      }
    ]
