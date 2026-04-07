namespace EffSharp.Gen.Tests

open System
open System.IO
open Expecto

module LifecycleE2E =
  open Harness

  let private fixturesRoot =
    Path.Combine(__SOURCE_DIRECTORY__, "Fixtures")

  let private fixtureDirectory fixtureName =
    Path.Combine(fixturesRoot, fixtureName)

  let private cleanupDirectory path =
    try
      if Directory.Exists(path) then
        Directory.Delete(path, true)
    with :? DirectoryNotFoundException ->
      ()

  let rec private copyDirectory sourceDirectory targetDirectory =
    Directory.CreateDirectory(targetDirectory) |> ignore

    for filePath in Directory.GetFiles(sourceDirectory) do
      let targetPath = Path.Combine(targetDirectory, Path.GetFileName(filePath))
      File.Copy(filePath, targetPath, true)

    for directoryPath in Directory.GetDirectories(sourceDirectory) do
      let targetPath = Path.Combine(targetDirectory, Path.GetFileName(directoryPath))
      copyDirectory directoryPath targetPath

  let tests =
    testSequenced <| testList "LifecycleE2E" [
      testTask "deleting the file that declares [<Effect>] interfaces removes stale generated files on rebuild" {
        let sourceFixtureName = "DeletionRemovesGenerated"
        let sourceDirectory = fixtureDirectory sourceFixtureName
        let tempFixtureName = $"{sourceFixtureName}-{Guid.NewGuid():N}"
        let tempDirectory = fixtureDirectory tempFixtureName
        let projectPath = Path.Combine(tempDirectory, $"{sourceFixtureName}.fsproj")
        let generatedDirectory = Path.Combine(tempDirectory, "obj", "Debug", "net10.0", "Gen")
        let effectsFilePath = Path.Combine(tempDirectory, "Effects.fs")

        cleanupDirectory tempDirectory
        copyDirectory sourceDirectory tempDirectory

        try
          let! firstBuild = buildProject projectPath
          Expect.equal firstBuild.ExitCode 0 $"fixture copy {tempFixtureName} should build successfully before deleting the effect source file. Output:{System.Environment.NewLine}{firstBuild.Output}"
          Expect.isTrue (File.Exists(Path.Combine(generatedDirectory, "Clock.g.fs"))) "the initial build should emit Clock.g.fs from the deleted effect source file"
          Expect.isTrue (File.Exists(Path.Combine(generatedDirectory, "Logger.g.fs"))) "the initial build should emit Logger.g.fs from the deleted effect source file"

          let projectText = File.ReadAllText(projectPath)
          let updatedProjectText = projectText.Replace("""    <Compile Include="Effects.fs" />""" + Environment.NewLine, "")
          File.WriteAllText(projectPath, updatedProjectText)
          File.Delete(effectsFilePath)

          let! secondBuild = buildProject projectPath
          Expect.equal secondBuild.ExitCode 0 $"fixture copy {tempFixtureName} should rebuild successfully after removing the effect source file from the project. Output:{System.Environment.NewLine}{secondBuild.Output}"

          let generatedFiles =
            if Directory.Exists(generatedDirectory) then
              Directory.GetFiles(generatedDirectory, "*.g.fs") |> Array.map Path.GetFileName |> Set.ofArray
            else
              Set.empty

          Expect.isFalse (generatedFiles.Contains("Clock.g.fs")) "Clock.g.fs should be removed after the source effect file is deleted from the project"
          Expect.isFalse (generatedFiles.Contains("Logger.g.fs")) "Logger.g.fs should be removed after the source effect file is deleted from the project"

          let! runResult = runBuiltFunction projectPath "DeletionRemovesGenerated.Program" "run"
          Expect.equal runResult.ExitCode 0 $"fixture copy {tempFixtureName} should still run after removing the effect source file. Output:{System.Environment.NewLine}{runResult.Output}"
          Expect.stringContains runResult.Output "deletion-removes-generated-runtime-ok" "runtime verification should prove the project still builds and runs without stale generated files"
        finally
          cleanupDirectory tempDirectory
      }
    ]
