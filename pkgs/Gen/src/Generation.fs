namespace EffSharp.Gen

open System
open System.IO
open System.Text

type GenerationRequest = {
  ProjectDirectory: string
  IntermediateOutputPath: string
  CompileInputs: CompileInput array
  ParseCommandLineArgs: string list
  OtherFlags: string
}

type GenerationResult = {
  Diagnostics: EffectDiagnostic list
  GeneratedFiles: GeneratedFile array
  OrderedCompileItems: string array
}

module Generation =
  let private writeAllTextIfChanged path contents =
    if not (File.Exists(path)) || File.ReadAllText(path) <> contents then
      File.WriteAllText(path, contents)

  let syncGeneratedFiles (effectInterfaces: EffectInterface list) (outputDirectory: string) =
    Directory.CreateDirectory(outputDirectory) |> ignore

    let generatedFiles =
      effectInterfaces
      |> List.map (fun effectInterface ->
        let outputPath = Path.Combine(outputDirectory, $"{effectInterface.ServiceName}.g.fs")

        {
          SourceFile = effectInterface.SourceFile
          OutputPath = outputPath
          Contents = Emission.emitFile effectInterface
        })
      |> List.toArray

    let expectedFiles =
      generatedFiles
      |> Array.map _.OutputPath
      |> Set.ofArray

    for staleFile in Directory.GetFiles(outputDirectory, "*.g.fs") do
      if not (Set.contains staleFile expectedFiles) then
        File.Delete(staleFile)

    for generatedFile in generatedFiles do
      writeAllTextIfChanged generatedFile.OutputPath generatedFile.Contents

    generatedFiles

  let orderedCompileItems (compileInputs: CompileInput array) (generatedFiles: GeneratedFile array) =
    let generatedBySource =
      generatedFiles
      |> Array.groupBy _.SourceFile
      |> Map.ofArray

    let orderedItems = ResizeArray<string>()
    let generatedInputs, sourceInputs =
      compileInputs
      |> Array.partition _.IsGenerated

    for generatedInput in generatedInputs do
      orderedItems.Add(generatedInput.FullPath)

    for compileInput in sourceInputs do
      orderedItems.Add(compileInput.FullPath)

      match Map.tryFind compileInput.FullPath generatedBySource with
      | Some generatedForSource ->
          for generatedFile in generatedForSource do
            orderedItems.Add(generatedFile.OutputPath)
      | None -> ()

    orderedItems.ToArray()

  let parseOtherFlags (otherFlags: string) =
    let tokens = ResizeArray<string>()
    let current = StringBuilder()
    let mutable inQuotes = false

    let flushCurrent () =
      if current.Length > 0 then
        tokens.Add(current.ToString())
        current.Clear() |> ignore

    for ch in otherFlags do
      match ch with
      | '"' ->
          inQuotes <- not inQuotes
      | c when Char.IsWhiteSpace(c) && not inQuotes ->
          flushCurrent ()
      | c ->
          current.Append(c) |> ignore

    flushCurrent ()
    tokens |> Seq.toList

  let run (request: GenerationRequest) =
    let outputDirectory =
      ProjectInputs.generatedOutputDirectory request.ProjectDirectory request.IntermediateOutputPath

    let sourceCompileInputs =
      request.CompileInputs
      |> Array.filter (_.IsGenerated >> not)

    let sourceFiles =
      sourceCompileInputs
      |> Array.map _.FullPath
      |> Array.toList

    let parseCommandLineArgs =
      request.ParseCommandLineArgs @ parseOtherFlags request.OtherFlags

    let parsedFiles =
      sourceCompileInputs
      |> Array.map (fun compileInput ->
        FcsParsing.parseFile sourceFiles parseCommandLineArgs compileInput.FullPath)

    let validation = Validation.validateFiles parsedFiles

    if validation.Diagnostics.IsEmpty then
      let generatedFiles = syncGeneratedFiles validation.Interfaces outputDirectory

      {
        Diagnostics = []
        GeneratedFiles = generatedFiles
        OrderedCompileItems = orderedCompileItems request.CompileInputs generatedFiles
      }
    else
      syncGeneratedFiles [] outputDirectory |> ignore

      {
        Diagnostics = validation.Diagnostics
        GeneratedFiles = [||]
        OrderedCompileItems = [||]
      }
