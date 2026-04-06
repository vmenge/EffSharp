namespace EffFs.EffectGen

open System
open System.IO
open System.Text
open Microsoft.Build.Framework
open Microsoft.Build.Utilities

type GenerateEffectFilesTask() =
  inherit Task()

  member private _.clearGeneratedFiles (outputDirectory: string) =
    Directory.CreateDirectory(outputDirectory) |> ignore

    for staleFile in Directory.GetFiles(outputDirectory, "*.g.fs") do
      File.Delete(staleFile)

  [<Required>]
  member val ProjectDirectory = "" with get, set

  [<Required>]
  member val IntermediateOutputPath = "" with get, set

  [<Required>]
  member val CompileItems : ITaskItem array = [||] with get, set

  member val ParseCommandLineArgs : ITaskItem array = [||] with get, set

  member val OtherFlags = "" with get, set

  [<Output>]
  member val GeneratedFiles : ITaskItem array = [||] with get, set

  [<Output>]
  member val OrderedCompileItems : ITaskItem array = [||] with get, set

  member private _.logDiagnostic (diagnostic: EffectDiagnostic) =
    base.Log.LogError(
      (null: string),
      diagnostic.Code,
      (null: string),
      diagnostic.FilePath,
      diagnostic.Line,
      diagnostic.Column,
      diagnostic.Line,
      diagnostic.Column,
      diagnostic.Message,
      [||]
    )

  member private this.generate (effectInterfaces: EffectInterface list) (outputDirectory: string) =
    this.clearGeneratedFiles outputDirectory

    let generatedFiles =
      effectInterfaces
      |> List.map (fun effectInterface ->
        let outputPath = Path.Combine(outputDirectory, $"{effectInterface.EnvironmentName}.g.fs")

        {
          SourceFile = effectInterface.SourceFile
          OutputPath = outputPath
          Contents = Emission.emitFile effectInterface
        })
      |> List.toArray

    for generatedFile in generatedFiles do
      File.WriteAllText(generatedFile.OutputPath, generatedFile.Contents)

    generatedFiles

  member private _.orderedCompileItems (compileInputs: CompileInput array) (generatedFiles: GeneratedFile array) =
    let generatedBySource =
      generatedFiles
      |> Array.groupBy _.SourceFile
      |> Map.ofArray

    let orderedItems = ResizeArray<ITaskItem>()

    for compileInput in compileInputs do
      orderedItems.Add(TaskItem(compileInput.FullPath) :> ITaskItem)

      match Map.tryFind compileInput.FullPath generatedBySource with
      | Some generatedForSource ->
          for generatedFile in generatedForSource do
            orderedItems.Add(TaskItem(generatedFile.OutputPath) :> ITaskItem)
      | None -> ()

    orderedItems.ToArray()

  member private this.parseCommandLineArgs () =
    let argsFromItems =
      this.ParseCommandLineArgs
      |> Array.map _.ItemSpec
      |> Array.toList

    let argsFromOtherFlags =
      let tokens = ResizeArray<string>()
      let current = StringBuilder()
      let mutable inQuotes = false

      let flushCurrent () =
        if current.Length > 0 then
          tokens.Add(current.ToString())
          current.Clear() |> ignore

      for ch in this.OtherFlags do
        match ch with
        | '"' ->
            inQuotes <- not inQuotes
        | c when Char.IsWhiteSpace(c) && not inQuotes ->
            flushCurrent ()
        | c ->
            current.Append(c) |> ignore

      flushCurrent ()
      tokens |> Seq.toList

    argsFromItems @ argsFromOtherFlags

  override this.Execute() =
    try
      let compileInputs = ProjectInputs.compileInputs this.ProjectDirectory this.CompileItems
      let outputDirectory = ProjectInputs.generatedOutputDirectory this.ProjectDirectory this.IntermediateOutputPath
      let sourceFiles = compileInputs |> Array.map _.FullPath |> Array.toList
      let parseCommandLineArgs = this.parseCommandLineArgs ()
      let parsedFiles =
        compileInputs
        |> Array.map (fun compileInput -> FcsParsing.parseFile sourceFiles parseCommandLineArgs compileInput.FullPath)

      let validation = Validation.validateFiles parsedFiles

      if validation.Diagnostics.IsEmpty then
        let generatedFiles = this.generate validation.Interfaces outputDirectory

        this.GeneratedFiles <-
          generatedFiles
          |> Array.map (fun generatedFile -> TaskItem(generatedFile.OutputPath) :> ITaskItem)

        this.OrderedCompileItems <- this.orderedCompileItems compileInputs generatedFiles
        true
      else
        this.clearGeneratedFiles outputDirectory

        for diagnostic in validation.Diagnostics do
          this.logDiagnostic diagnostic

        this.GeneratedFiles <- [||]
        this.OrderedCompileItems <- [||]
        false
    with error ->
      this.Log.LogErrorFromException(error, true, true, null)
      false
