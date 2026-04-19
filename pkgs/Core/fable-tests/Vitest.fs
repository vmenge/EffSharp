namespace EffSharp.Core.FableTests

open Fable.Core

module Vitest =
  [<Import("describe", "vitest")>]
  let describe (_name: string) (_body: unit -> unit) : unit = jsNative

  [<Import("it", "vitest")>]
  let it (_name: string) (_body: unit -> unit) : unit = jsNative

  [<Import("it", "vitest")>]
  let itp (_name: string) (_body: unit -> Await<unit>) : unit = jsNative

module Expect =
  [<Import("expect", "vitest")>]
  let private expectJs (_actual: obj) : obj = jsNative

  [<Emit("$0.toEqual($1)")>]
  let private toEqual (_matcher: obj) (_expected: obj) : unit = jsNative

  [<Emit("$0.toBe($1)")>]
  let private toBe (_matcher: obj) (_expected: obj) : unit = jsNative

  let equal actual expected =
    expectJs (box actual) |> fun matcher -> toEqual matcher (box expected)

  let isTrue actual =
    expectJs (box actual) |> fun matcher -> toBe matcher true

  let arrEqual (actual: 'a array) (expected: 'a array) =
    equal actual expected
