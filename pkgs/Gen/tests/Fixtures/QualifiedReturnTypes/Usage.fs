namespace QualifiedReturnTypesRed

open EffSharp.Core

module Usage =
  let parseProgram () : Eff<int, ParseError, #IParser> = IParser.Parse "42"
  let fetchProgram () : Eff<Response, exn, #IHttp> = IHttp.Fetch "/users"
  let tryFetchProgram () : Eff<Response, HttpError, #IHttp> = IHttp.TryFetch "/users"
  let loadProgram () : Eff<Model, exn, #IStore> = IStore.Load "42"
  let tryLoadProgram () : Eff<Model, StoreError, #IStore> = IStore.TryLoad "42"
  let readProgram () : Eff<string, exn, #IFileSystem> = IFileSystem.Read "file.txt"
  let tryReadProgram () : Eff<string, FileError, #IFileSystem> = IFileSystem.TryRead "file.txt"
  let spawnProgram () : Eff<JobHandle<JobResult>, SpawnError, #IRuntime> = IRuntime.Spawn { Id = "job-1" }
