namespace QualifiedReturnTypesRed

type Response = {
  StatusCode: int
}

type HttpError =
  | Unreachable

type Model = {
  Id: string
}

type StoreError =
  | Missing

type FileError =
  | PermissionDenied

type ParseError =
  | InvalidInput

type Job = {
  Id: string
}

type JobResult = {
  ExitCode: int
}

type JobHandle<'t> = {
  Result: 't option
}

type SpawnError =
  | TimedOut
