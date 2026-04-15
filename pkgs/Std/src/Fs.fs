namespace EffSharp.Std

open EffSharp.Core
open EffSharp.Gen

type FsErr =
  | NotFound of string
  | PermissionDenied of string
  | AlreadyExists of string
  | IOError of exn

// TODO
type DirEntry = struct end

// TODO
type Metadata = struct end

// TODO
type Permissions = struct end

// TODO
type FileTimes = struct end

// TODO
type FileHandle = struct end

[<Struct>]
type FileAccess =
  | Read
  | Write
  | ReadWrite
  | Append

[<Struct>]
type FileCreate =
  | Open
  | Create
  | CreateNew
  | OpenOrCreate
  | Truncate

[<Struct>]
type FileOpen = {
  Access: FileAccess
  Create: FileCreate
}

[<Effect(Mode.Wrap)>]
type Fs =
  abstract readText: Path -> Eff<string, FsErr, unit>
  abstract read: Path -> Eff<byte array, FsErr, unit>

  abstract writeText: Path -> string -> Eff<unit, FsErr, unit>
  abstract write: Path -> byte array -> Eff<unit, FsErr, unit>

  abstract appendText: Path -> string -> Eff<unit, FsErr, unit>
  abstract append: Path -> byte array -> Eff<unit, FsErr, unit>

  abstract readDir: Path -> Eff<DirEntry array, FsErr, unit>
  abstract createDir: Path -> Eff<unit, FsErr, unit>
  abstract createDirAll: Path -> Eff<unit, FsErr, unit>
  abstract removeDir: Path -> Eff<unit, FsErr, unit>
  abstract removeDirAll: Path -> Eff<unit, FsErr, unit>
  abstract removeFile: Path -> Eff<unit, FsErr, unit>
  abstract copy: Path -> Path -> Eff<unit, FsErr, unit>
  abstract rename: Path -> Path -> Eff<unit, FsErr, unit>

  abstract metadata: Path -> Eff<Metadata, FsErr, unit>
  abstract exists: Path -> Eff<bool, FsErr, unit>
  abstract isFile: Path -> Eff<bool, FsErr, unit>
  abstract isDir: Path -> Eff<bool, FsErr, unit>
  abstract canonicalize: Path -> Eff<Path, FsErr, unit>

  abstract hardLink: link: Path -> original: Path -> Eff<unit, FsErr, unit>
  abstract symlinkFile: link: Path -> original: Path -> Eff<unit, FsErr, unit>
  abstract symlinkDir: link: Path -> original: Path -> Eff<unit, FsErr, unit>
  abstract symlinkMetadata: link: Path -> Eff<Metadata, FsErr, unit>
  abstract readLink: link: Path -> Eff<Path, FsErr, unit>

  abstract setPermissions: Path -> Permissions -> Eff<unit, FsErr, unit>
  abstract setTimes: Path -> FileTimes -> Eff<unit, FsErr, unit>

  abstract withFile:
    Path ->
    FileOpen ->
    (FileHandle -> Eff<'t, FsErr, unit>) ->
      Eff<'t, FsErr, unit>

