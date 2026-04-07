namespace SupportedAsyncRed

open EffSharp.Core

module Usage =
  let fetchProgram () : Eff<Response, exn, #IHttp> = IHttp.Fetch "/users"
  let tryFetchProgram () : Eff<Response, HttpError, #IHttp> = IHttp.TryFetch "/users"
  let loadProgram () : Eff<Model, exn, #IStore> = IStore.Load "42"
  let tryLoadProgram () : Eff<Model, StoreError, #IStore> = IStore.TryLoad "42"
  let readProgram () : Eff<string, exn, #IFileSystem> = IFileSystem.Read "file.txt"
  let tryReadProgram () : Eff<string, FileError, #IFileSystem> = IFileSystem.TryRead "file.txt"
