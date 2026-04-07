namespace EffSharp.Gen.Tests

open System.IO
open Expecto

module ModeCoverageE2E =
  open Harness

  let private fixtureDirectory fixtureName =
    Path.Combine(__SOURCE_DIRECTORY__, "Fixtures", fixtureName)

  let private fixtureProject fixtureName =
    Path.Combine(fixtureDirectory fixtureName, $"{fixtureName}.fsproj")

  let private generatedDirectory fixtureName =
    Path.Combine(fixtureDirectory fixtureName, "obj", "Debug", "net10.0", "Gen")

  let private cleanupGeneratedDirectory fixtureName =
    try
      let path = generatedDirectory fixtureName

      if Directory.Exists(path) then
        Directory.Delete(path, true)
    with :? DirectoryNotFoundException ->
      ()

  let tests =
    testSequenced <| testList "ModeCoverageE2E" [
      testTask "direct mode keeps a non-interface-prefixed type name for both module and environment" {
        let fixtureName = "DirectNonInterfacePrefix"
        cleanupGeneratedDirectory fixtureName

        let! result = buildProject (fixtureProject fixtureName)

        Expect.equal result.ExitCode 0 $"fixture {fixtureName} should build successfully when direct generation keeps the tagged type name intact. Output:{System.Environment.NewLine}{result.Output}"

        let generatedText =
          Directory.GetFiles(generatedDirectory fixtureName, "*.g.fs")
          |> Array.sort
          |> Array.map File.ReadAllText
          |> String.concat System.Environment.NewLine

        Expect.stringContains generatedText "type Logger with" "direct generation should emit a type extension for the tagged type"
        Expect.stringContains generatedText "static member Debug (arg1: string) : EffSharp.Core.Eff<unit, 'e, #Logger>" "direct generation should use the tagged type itself as the environment constraint without rewriting the member name"
        Expect.isFalse (generatedText.Contains("type ELogger =")) "direct generation should not synthesize a wrapper interface for non-I-prefixed types"

        let! runResult = runBuiltFunction (fixtureProject fixtureName) "DirectNonInterfacePrefixRed.Program" "run"
        Expect.equal runResult.ExitCode 0 $"fixture {fixtureName} should run successfully. Output:{System.Environment.NewLine}{runResult.Output}"
        Expect.stringContains runResult.Output "direct-non-interface-prefix-runtime-ok" "runtime verification should exercise the direct module for a non-I-prefixed type"
      }

      testTask "wrapped mode keeps source callable types while emitting wrapper contracts under the nested Effect namespace" {
        let fixtureName = "WrappedNaming"
        cleanupGeneratedDirectory fixtureName

        let! result = buildProject (fixtureProject fixtureName)

        Expect.equal result.ExitCode 0 $"fixture {fixtureName} should build successfully when wrapped generation applies all naming branches. Output:{System.Environment.NewLine}{result.Output}"

        let generatedText =
          Directory.GetFiles(generatedDirectory fixtureName, "*.g.fs")
          |> Array.sort
          |> Array.map File.ReadAllText
          |> String.concat System.Environment.NewLine

        Expect.stringContains generatedText "namespace WrappedNamingRed.Effect" "wrapped generation should emit wrapper contracts into the nested Effect namespace so multiple generated files can contribute safely"
        Expect.stringContains generatedText "type IGreeter with" "wrapped generation should keep the source interface as the callable type for I-prefixed interfaces"
        Expect.stringContains generatedText "type Greeter =" "wrapped generation should strip the interface prefix for the wrapper type inside Effect when the second character is uppercase"
        Expect.stringContains generatedText "abstract Greeter: WrappedNamingRed.IGreeter" "I-prefixed wrapped generation should still expose the stripped property name through the nested wrapper type"
        Expect.stringContains generatedText "static member Greet (arg1: string) : EffSharp.Core.Eff<string, 'e, #Effect.Greeter>" "wrapped generation should use the nested Effect.Greeter environment for I-prefixed interfaces without rewriting the member name"

        Expect.stringContains generatedText "type Logger with" "wrapped generation should keep the source type as the callable type for non-I-prefixed interfaces"
        Expect.stringContains generatedText "type Logger =" "wrapped generation should preserve the original type name inside Effect for non-I-prefixed interfaces"
        Expect.stringContains generatedText "abstract Logger: WrappedNamingRed.Logger" "non-I-prefixed wrapped generation should preserve the original property name through the nested wrapper type"
        Expect.stringContains generatedText "static member Debug (arg1: string) : EffSharp.Core.Eff<unit, 'e, #Effect.Logger>" "wrapped generation should use the nested Effect.Logger environment for non-I-prefixed interfaces without rewriting the member name"

        Expect.stringContains generatedText "type Ilogger with" "wrapped generation should keep the source type as the callable type even for I+lowercase edge cases"
        Expect.stringContains generatedText "type Ilogger =" "wrapped generation should preserve the full type name inside Effect when the second character is not uppercase"
        Expect.stringContains generatedText "abstract Ilogger: WrappedNamingRed.Ilogger" "I+lowercase wrapped generation should preserve the full property name through the nested wrapper type"
        Expect.stringContains generatedText "static member Trace (arg1: string) : EffSharp.Core.Eff<unit, 'e, #Effect.Ilogger>" "I+lowercase wrapped generation should use the nested Effect.Ilogger environment without rewriting the member name"

        let! runResult = runBuiltFunction (fixtureProject fixtureName) "WrappedNamingRed.Program" "run"
        Expect.equal runResult.ExitCode 0 $"fixture {fixtureName} should run successfully. Output:{System.Environment.NewLine}{runResult.Output}"
        Expect.stringContains runResult.Output "wrapped-naming-runtime-ok" "runtime verification should exercise the wrapped naming branches"
      }

      testTask "direct and wrapped modes can coexist in one project without changing callable type naming" {
        let fixtureName = "MixedModes"
        cleanupGeneratedDirectory fixtureName

        let! result = buildProject (fixtureProject fixtureName)

        Expect.equal result.ExitCode 0 $"fixture {fixtureName} should build successfully when direct and wrapped generation coexist. Output:{System.Environment.NewLine}{result.Output}"

        let generatedText =
          Directory.GetFiles(generatedDirectory fixtureName, "*.g.fs")
          |> Array.sort
          |> Array.map File.ReadAllText
          |> String.concat System.Environment.NewLine

        Expect.stringContains generatedText "type ILogger with" "direct generation should keep the source interface as the callable type in mixed-mode projects"
        Expect.stringContains generatedText "static member Info (arg1: string) : EffSharp.Core.Eff<unit, 'e, #ILogger>" "direct generation should still use the tagged interface as the environment constraint in mixed-mode projects without rewriting the member name"
        Expect.isFalse (generatedText.Contains("type ELogger =")) "direct generation should not emit the old E-prefixed wrapper interface for ILogger in mixed-mode projects"

        Expect.stringContains generatedText "type IClock with" "wrapped generation should keep the source interface as the callable type in mixed-mode projects"
        Expect.stringContains generatedText "namespace MixedModesRed.Effect" "wrapped generation should emit wrapper contracts into the nested Effect namespace in mixed-mode projects"
        Expect.stringContains generatedText "type Clock =" "wrapped generation should still emit the nested Effect.Clock wrapper type in mixed-mode projects"
        Expect.stringContains generatedText "abstract Clock: MixedModesRed.IClock" "wrapped generation should still emit the service property in mixed-mode projects"
        Expect.stringContains generatedText "static member Now () : EffSharp.Core.Eff<string, 'e, #Effect.Clock>" "wrapped generation should still use the nested Effect.Clock environment in mixed-mode projects without rewriting the member name"

        let! runResult = runBuiltFunction (fixtureProject fixtureName) "MixedModesRed.Program" "run"
        Expect.equal runResult.ExitCode 0 $"fixture {fixtureName} should run successfully. Output:{System.Environment.NewLine}{runResult.Output}"
        Expect.stringContains runResult.Output "mixed-modes-runtime-ok" "runtime verification should exercise both direct and wrapped generation in one project"
      }
    ]
