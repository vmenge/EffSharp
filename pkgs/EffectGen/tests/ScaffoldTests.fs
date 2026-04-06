namespace EffFs.EffectGen.Tests

open System.IO
open Expecto

module ScaffoldTests =
  let private repoRoot =
    Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", "..", ".."))

  let private effectGenRoot =
    Path.Combine(repoRoot, "pkgs", "EffectGen")

  let private effectGenProject =
    Path.Combine(effectGenRoot, "src", "EffectGen.fsproj")

  let private solutionPath =
    Path.Combine(repoRoot, "EffFs.slnx")

  let private readAllText path = File.ReadAllText(path)

  let tests =
    testList "Scaffold" [
      testCase "solution contains EffectGen projects" (fun () ->
        let solution = readAllText solutionPath

        Expect.stringContains solution "pkgs/EffectGen/src/EffectGen.fsproj" "solution should include the EffectGen package project"
        Expect.stringContains solution "pkgs/EffectGen/tool/EffectGen.Tool.fsproj" "solution should include the EffectGen source-mode tool project"
        Expect.stringContains solution "pkgs/EffectGen/tests/EffectGen.Tests.fsproj" "solution should include the EffectGen test project"
      )

      testCase "buildTransitive asset files exist" (fun () ->
        let propsPath = Path.Combine(effectGenRoot, "buildTransitive", "EffectGen.props")
        let targetsPath = Path.Combine(effectGenRoot, "buildTransitive", "EffectGen.targets")

        Expect.isTrue (File.Exists(propsPath)) "EffectGen.props should exist for transitive MSBuild wiring"
        Expect.isTrue (File.Exists(targetsPath)) "EffectGen.targets should exist for transitive MSBuild wiring"
      )

      testCase "package project includes buildTransitive assets for packing" (fun () ->
        let projectText = readAllText effectGenProject

        Expect.stringContains projectText "buildTransitive" "package project should reference buildTransitive assets"
        Expect.stringContains projectText "PackagePath=\"buildTransitive" "buildTransitive assets should be packed into the correct package path"
      )
    ]
