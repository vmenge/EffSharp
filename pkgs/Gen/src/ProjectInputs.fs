namespace EffSharp.Gen

open System
open System.IO
open Microsoft.Build.Framework

type CompileInput = {
  ItemSpec: string
  FullPath: string
  IsGenerated: bool
}

module ProjectInputs =
  let private normalizePath (projectDirectory: string) (path: string) =
    if Path.IsPathRooted(path) then
      Path.GetFullPath(path)
    else
      Path.GetFullPath(Path.Combine(projectDirectory, path))

  let private generatedPathDetector (projectDirectory: string) =
    let intermediatePath =
      let rawPath = normalizePath projectDirectory "obj"
      rawPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)

    fun (fullPath: string) ->
      let candidate = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)

      candidate.StartsWith(intermediatePath + string Path.DirectorySeparatorChar, StringComparison.Ordinal)
      || candidate.StartsWith(intermediatePath + string Path.AltDirectorySeparatorChar, StringComparison.Ordinal)

  let ofPaths (projectDirectory: string) (paths: string seq) =
    let isGeneratedPath = generatedPathDetector projectDirectory

    paths
    |> Seq.map (fun path ->
      let fullPath = normalizePath projectDirectory path

      {
        ItemSpec = path
        FullPath = fullPath
        IsGenerated = isGeneratedPath fullPath
      })
    |> Seq.toArray

  let compileInputs (projectDirectory: string) (compileItems: ITaskItem array) =
    let isGeneratedPath = generatedPathDetector projectDirectory

    compileItems
    |> Array.map (fun item ->
      let itemSpec = item.ItemSpec
      let fullPath =
        let metadataPath = item.GetMetadata("FullPath")

        if String.IsNullOrWhiteSpace(metadataPath) then
          normalizePath projectDirectory itemSpec
        else
          normalizePath projectDirectory metadataPath

      {
        ItemSpec = itemSpec
        FullPath = fullPath
        IsGenerated = isGeneratedPath fullPath
      })

  let generatedOutputDirectory (projectDirectory: string) (intermediateOutputPath: string) =
    normalizePath projectDirectory intermediateOutputPath
    |> fun path -> Path.Combine(path, "Gen")
