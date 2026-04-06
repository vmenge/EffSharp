namespace SupportedEffProvideFromRed

type Job = {
  Id: int
}

type JobResult = {
  Value: int
}

type SpawnError =
  | Forbidden

type JobHandle<'t> = {
  Id: int
  Result: 't option
}
