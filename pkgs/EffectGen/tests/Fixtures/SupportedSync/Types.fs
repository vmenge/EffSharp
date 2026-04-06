namespace SupportedSyncRed

type ParseError =
  | InvalidInput

type LookupError =
  | NotFound

type User = {
  Id: int
  Name: string
}
