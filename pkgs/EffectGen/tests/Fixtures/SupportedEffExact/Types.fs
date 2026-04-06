namespace SupportedEffExactRed

type Job = {
  Id: int
}

type JobResult = {
  ExitCode: int
}

type SpawnError =
  | Busy

type JobHandle<'t> = {
  Id: int
  Result: 't option
}
