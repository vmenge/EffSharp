namespace EffFs.EffectGen.Tests

open System.Diagnostics
open System.Threading.Tasks

module Harness =
  type BuildResult = {
    ExitCode: int
    Output: string
  }

  let buildProject (projectPath: string) : Task<BuildResult> = task {
    let startInfo = ProcessStartInfo("dotnet", $"build \"{projectPath}\" --nologo")
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.UseShellExecute <- false

    use proc = new Process(StartInfo = startInfo)

    if not (proc.Start()) then
      failwith $"failed to start build for {projectPath}"

    do! proc.WaitForExitAsync()

    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()

    return {
      ExitCode = proc.ExitCode
      Output = stdout + stderr
    }
  }
