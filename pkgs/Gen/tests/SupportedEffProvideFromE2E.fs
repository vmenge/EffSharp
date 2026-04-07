namespace EffSharp.Gen.Tests

open System.IO
open Expecto

module SupportedEffProvideFromE2E =
  open Harness

  let private fixtureName = "SupportedEffProvideFrom"

  let private fixtureDirectory =
    Path.Combine(__SOURCE_DIRECTORY__, "Fixtures", fixtureName)

  let private fixtureProject =
    Path.Combine(fixtureDirectory, $"{fixtureName}.fsproj")

  let private generatedDirectory =
    Path.Combine(fixtureDirectory, "obj", "Debug", "net10.0", "Gen")

  let private cleanupGeneratedDirectory () =
    try
      if Directory.Exists(generatedDirectory) then
        Directory.Delete(generatedDirectory, true)
    with :? DirectoryNotFoundException ->
      ()

  let private builtFixture =
    lazy (
      task {
        cleanupGeneratedDirectory ()
        return! buildProject fixtureProject
      })

  let tests =
    testSequenced <| testList "SupportedEffProvideFromE2E" [
      testTask "supported Eff provideFrom fixture builds with explicit wrapped generation in the same build" {
        let! result = builtFixture.Value

        Expect.equal result.ExitCode 0 $"fixture {fixtureName} should build successfully once mechanical Eff adaptation exists. Output:{System.Environment.NewLine}{result.Output}"
      }

      testTask "supported Eff provideFrom generated output keeps the source callable type and upcasts through provideFrom before flattening" {
        let! result = builtFixture.Value

        Expect.equal result.ExitCode 0 $"fixture {fixtureName} should build successfully before inspecting generated output. Output:{System.Environment.NewLine}{result.Output}"

        let generatedText =
          Directory.GetFiles(generatedDirectory, "*.g.fs")
          |> Array.sort
          |> Array.map File.ReadAllText
          |> String.concat System.Environment.NewLine

        Expect.stringContains generatedText "namespace SupportedEffProvideFromRed.Effect" "wrapped generation should emit wrapper contracts into the nested Effect namespace so multiple generated files can contribute safely"
        Expect.stringContains generatedText "type RuntimeService =" "wrapped generation should emit the nested Effect.RuntimeService wrapper type"
        Expect.stringContains generatedText "type IRuntimeService with" "wrapped generation should keep the source interface as the callable type"
        Expect.stringContains generatedText "inherit SupportedEffProvideFromRed.IRuntimeEnv" "The generated environment interface should inherit the mechanically matching inner environment"
        Expect.stringContains generatedText "static member Spawn (arg1: SupportedEffProvideFromRed.Job) : EffSharp.Core.Eff<SupportedEffProvideFromRed.JobHandle<SupportedEffProvideFromRed.JobResult>, SupportedEffProvideFromRed.SpawnError, #Effect.RuntimeService>" "wrapped generation should expose the nested Effect.RuntimeService environment on the effect type without rewriting the member name"
        Expect.stringContains generatedText "|> Eff.map (Eff.provideFrom (fun (outer: #Effect.RuntimeService) -> outer :> SupportedEffProvideFromRed.IRuntimeEnv))" "The nested Eff should be adapted through a direct upcast before flattening"
        Expect.stringContains generatedText "|> Eff.flatten" "The adapted nested Eff should then be flattened"
      }

      testTask "supported Eff provideFrom fixture executes generated modules at runtime" {
        let! buildResult = builtFixture.Value
        Expect.equal buildResult.ExitCode 0 $"fixture {fixtureName} should build successfully before runtime verification. Output:{System.Environment.NewLine}{buildResult.Output}"

        let! runResult = runBuiltFunction fixtureProject "SupportedEffProvideFromRed.Program" "run"
        Expect.equal runResult.ExitCode 0 $"fixture {fixtureName} should run successfully. Output:{System.Environment.NewLine}{runResult.Output}"
        Expect.stringContains runResult.Output "supported-eff-providefrom-runtime-ok" "runtime verification should exercise the generated provideFrom wrapper"
      }
    ]
