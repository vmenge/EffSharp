namespace EffFs.EffectGen.Tests

open System.Diagnostics
open System.IO
open System.Threading.Tasks

module Harness =
  type BuildResult = {
    ExitCode: int
    Output: string
  }

  let private repoRoot =
    Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", "..", ".."))

  let private coreProject =
    Path.Combine(repoRoot, "pkgs", "Core", "src", "Core.fsproj")

  let private effectGenProject =
    Path.Combine(repoRoot, "pkgs", "EffectGen", "src", "EffectGen.fsproj")

  let private runDotnet (workingDirectory: string option) (arguments: string) : Task<BuildResult> = task {
    let startInfo = ProcessStartInfo("dotnet", arguments)
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.UseShellExecute <- false

    match workingDirectory with
    | Some directory -> startInfo.WorkingDirectory <- directory
    | None -> ()

    use proc = new Process(StartInfo = startInfo)

    if not (proc.Start()) then
      failwith $"failed to start dotnet {arguments}"

    do! proc.WaitForExitAsync()

    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()

    return {
      ExitCode = proc.ExitCode
      Output = stdout + stderr
    }
  }

  let private ensureProjectBuild (projectPath: string) = task {
    let! result = runDotnet None $"build \"{projectPath}\" --nologo"

    if result.ExitCode <> 0 then
      failwith $"failed to build prerequisite project {projectPath}{System.Environment.NewLine}{result.Output}"
  }

  let buildProject (projectPath: string) : Task<BuildResult> = task {
    do! ensureProjectBuild coreProject
    do! ensureProjectBuild effectGenProject
    return! runDotnet None $"build \"{projectPath}\" --nologo -t:Rebuild"
  }

  let packProject (projectPath: string) (outputDirectory: string) : Task<BuildResult> = task {
    do! ensureProjectBuild coreProject
    do! ensureProjectBuild effectGenProject
    return! runDotnet None $"pack \"{projectPath}\" --nologo --no-build -p:Configuration=Debug -o \"{outputDirectory}\""
  }
