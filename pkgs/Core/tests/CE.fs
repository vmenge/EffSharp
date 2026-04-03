namespace EffFs.Core.Tests

module CE =
    open System
    open Expecto
    open EffFs.Core
    open System.Threading.Tasks

    type DisposeProbe() =
        let mutable disposed = false

        member _.Disposed = disposed

        interface IDisposable with
            member _.Dispose() = disposed <- true

    let tests =
        testList
            "CE"
            [ testTask "ce sources works" {
                  let! value =
                      eff {
                          let a = 1
                          let! b = Eff.value 2
                          let! c = Ok 3
                          let! d = Some 4
                          let! e = task { return 5 }
                          let! f = async { return 6 }
                          let! g = task { return Ok 7 }
                          let! h = async { return Ok 8 }

                          let result = a + b + c + d + e + f + g + h

                          return result
                      }
                      |> Eff.runTask ()

                  Expect.equal value (Ok 36) "should be equal"
              }

              testTask "return! eff works" {
                  let! value = eff { return! Eff.value 5 } |> Eff.runTask ()

                  Expect.equal value (Ok 5) "should return from Eff directly"
              }

              testTask "result error short-circuits" {
                  let mutable ran = false

                  let! value =
                      eff {
                          let! _ = Ok 1
                          let! _ = Error(exn "boom")
                          ran <- true
                          return 1
                      }
                      |> Eff.runTask ()

                  let err: exn = Result.error value
                  Expect.equal err.Message "boom" "should return the result error"
                  Expect.isFalse ran "later CE code should not run"
              }

              testTask "option none short-circuits" {
                  let mutable ran = false

                  let! value =
                      eff {
                          let! _ = Some 1
                          let! _ = None
                          ran <- true
                          return 1
                      }
                      |> Eff.runTask ()

                  let err: exn = Result.error value
                  Expect.equal (err.GetType()) typeof<ValueIsNone> "should return ValueIsNone"
                  Expect.isFalse ran "later CE code should not run"
              }

              testTask "task result error short-circuits" {
                  let mutable ran = false
                  let taskResult () : Task<Result<int, exn>> = task { return Error(exn "boom") }

                  let! value =
                      eff {
                          let! _ = taskResult ()
                          ran <- true
                          return 1
                      }
                      |> Eff.runTask ()

                  let err: exn = Result.error value
                  Expect.equal err.Message "boom" "should return the task result error"
                  Expect.isFalse ran "later CE code should not run"
              }

              testTask "exception thrown inside CE is caught" {
                  let! value =
                      eff {
                          let _ = failwith "boom"
                          return 1
                      }
                      |> Eff.runTask ()

                  let err: exn = Result.error value
                  Expect.equal err.Message "boom" "should catch thrown exceptions"
              }

              testTask "try finally runs on failure" {
                  let mutable cleaned = false

                  let! value =
                      eff {
                          try
                              let! _ = Eff.errwith "boom"
                              return 1
                          finally
                              cleaned <- true
                      }
                      |> Eff.runTask ()

                  let err: exn = Result.error value
                  Expect.equal err.Message "boom" "should preserve the body error"
                  Expect.isTrue cleaned "finally should run"
              }

              testTask "use disposes resources" {
                  let probe = new DisposeProbe()

                  let! value =
                      eff {
                          use _probe = probe
                          return 1
                      }
                      |> Eff.runTask ()

                  Expect.equal value (Ok 1) "should return the body result"
                  Expect.isTrue probe.Disposed "use should dispose the resource"
              } ]
