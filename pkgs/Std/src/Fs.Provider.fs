namespace EffSharp.Std

open EffSharp.Core

[<AutoOpen>]
module FsExt =
  type FsProvider internal () =
    interface Fs with
      member this.append
        (arg1: Path)
        (arg2: byte array)
        : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.appendText
        (arg1: Path)
        (arg2: string)
        : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.canonicalize(arg1: Path) : Eff<Path, FsErr, unit> =
        failwith "Not Implemented"

      member this.copy (arg1: Path) (arg2: Path) : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.createDir(arg1: Path) : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.createDirAll(arg1: Path) : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.exists(arg1: Path) : Eff<bool, FsErr, unit> =
        failwith "Not Implemented"

      member this.hardLink
        (link: Path)
        (original: Path)
        : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.isDir(arg1: Path) : Eff<bool, FsErr, unit> =
        failwith "Not Implemented"

      member this.isFile(arg1: Path) : Eff<bool, FsErr, unit> =
        failwith "Not Implemented"

      member this.metadata(arg1: Path) : Eff<Metadata, FsErr, unit> =
        failwith "Not Implemented"

      member this.read(arg1: Path) : Eff<byte array, FsErr, unit> =
        failwith "Not Implemented"

      member this.readDir(arg1: Path) : Eff<DirEntry array, FsErr, unit> =
        failwith "Not Implemented"

      member this.readLink(link: Path) : Eff<Path, FsErr, unit> =
        failwith "Not Implemented"

      member this.readText(arg1: Path) : Eff<string, FsErr, unit> =
        failwith "Not Implemented"

      member this.removeDir(arg1: Path) : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.removeDirAll(arg1: Path) : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.removeFile(arg1: Path) : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.rename (arg1: Path) (arg2: Path) : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.setPermissions
        (arg1: Path)
        (arg2: Permissions)
        : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.setTimes
        (arg1: Path)
        (arg2: FileTimes)
        : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.symlinkDir
        (link: Path)
        (original: Path)
        : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.symlinkFile
        (link: Path)
        (original: Path)
        : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.symlinkMetadata(link: Path) : Eff<Metadata, FsErr, unit> =
        failwith "Not Implemented"

      member this.withFile
        (arg1: Path)
        (arg2: FileOpen)
        (arg3: FileHandle -> Eff<'t, FsErr, unit>)
        : Eff<'t, FsErr, unit> =
        failwith "Not Implemented"

      member this.write
        (arg1: Path)
        (arg2: byte array)
        : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

      member this.writeText
        (arg1: Path)
        (arg2: string)
        : Eff<unit, FsErr, unit> =
        failwith "Not Implemented"

  type Fs with
    static member Provider() = FsProvider()
