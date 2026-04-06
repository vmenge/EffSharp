namespace EffFs.EffectGen.Tests

open System.IO
open Expecto

module SupportedEffProvideFromE2E =
  open Harness

  let private fixtureName = "SupportedEffProvideFrom"

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
    testSequenced <| testList "SupportedEffProvideFromE2E" [
      testTask "supported Eff provideFrom fixture builds with generated wrappers in the same build" {
        cleanupIntermediateDirectory ()

        let! result = buildProject fixtureProject

        Expect.equal result.ExitCode 0 $"fixture {fixtureName} should build successfully once mechanical Eff adaptation exists. Output:{System.Environment.NewLine}{result.Output}"
      }

      testTask "supported Eff provideFrom generated output upcasts through provideFrom before flattening" {
        cleanupIntermediateDirectory ()

        let! result = buildProject fixtureProject

        Expect.equal result.ExitCode 0 $"fixture {fixtureName} should build successfully before inspecting generated output. Output:{System.Environment.NewLine}{result.Output}"

        let generatedText =
          Directory.GetFiles(generatedDirectory, "*.g.fs")
          |> Array.sort
          |> Array.map File.ReadAllText
          |> String.concat System.Environment.NewLine

        Expect.stringContains generatedText "type ERuntimeService =" "The generated environment interface should exist for the service"
        Expect.stringContains generatedText "inherit IRuntimeEnv" "The generated environment interface should inherit the mechanically matching inner environment"
        Expect.stringContains generatedText "|> Eff.map (Eff.provideFrom (fun (outer: #ERuntimeService) -> outer :> IRuntimeEnv))" "The nested Eff should be adapted through a direct upcast before flattening"
        Expect.stringContains generatedText "|> Eff.flatten" "The adapted nested Eff should then be flattened"
      }
    ]
