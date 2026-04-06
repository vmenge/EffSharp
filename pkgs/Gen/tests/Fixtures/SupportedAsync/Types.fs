namespace SupportedAsyncRed

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
