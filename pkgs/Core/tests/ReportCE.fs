namespace EffFs.Core.Tests

module ReportCE =
    open Expecto
    open System.Threading.Tasks
    open EffFs.Core
    open EffFs.Core.ReportCE

    let tests =
        testList "ReportCE" [
            testTask "pure return can be annotated as exn error" {
                let program () : Eff<int, exn, unit> =
                    eff { return 1 }

                let! value = program () |> Eff.runTask ()
                Expect.equal value (Exit.Ok 1) "pure return should stay usable as exn effect"
            }

            testTask "let! normalizes Eff errors to Report" {
                let! value =
                    eff {
                        let! x = Eff.err "boom"
                        return x
                    }
                    |> Eff.runTask ()

                let err: exn = Exit.err value

                Expect.equal (err.GetType()) typeof<Report> "non-exn errors should be wrapped in Report"
                Expect.equal err.Message "boom" "report message should come from the original error"

                match err with
                |ReportAs (wrapped: string) ->
                    Expect.equal wrapped "boom" "report should preserve the original error payload"
                | _ ->
                    failtest "expected Report carrying the original string"
            }

            testTask "return! normalizes Eff errors to Report" {
                let! value =
                    eff {
                        return! Eff.err "boom"
                    }
                    |> Eff.runTask ()

                let err: exn = Exit.err value

                Expect.equal (err.GetType()) typeof<Report> "return! should normalize Eff errors"

                match err with
                | Report.ReportAs (wrapped: string) ->
                    Expect.equal wrapped "boom" "report should preserve the original error payload"
                | _ ->
                    failtest "expected Report carrying the original string"
            }

            testTask "result errors normalize to Report" {
                let! value =
                    eff {
                        let! _ = Error "boom"
                        return 1
                    }
                    |> Eff.runTask ()

                let err: exn = Exit.err value

                Expect.equal (err.GetType()) typeof<Report> "result errors should normalize to Report"

                match err with
                | Report.ReportAs (wrapped: string) ->
                    Expect.equal wrapped "boom" "report should preserve the original result error"
                | _ ->
                    failtest "expected Report carrying the original string"
            }

            testTask "task result errors normalize to Report" {
                let taskResult () : Task<Result<int, string>> = task {
                    return Error "boom"
                }

                let! value =
                    eff {
                        let! _ = taskResult ()
                        return 1
                    }
                    |> Eff.runTask ()

                let err: exn = Exit.err value

                Expect.equal (err.GetType()) typeof<Report> "task result errors should normalize to Report"

                match err with
                | Report.ReportAs (wrapped: string) ->
                    Expect.equal wrapped "boom" "report should preserve the original task result error"
                | _ ->
                    failtest "expected Report carrying the original string"
            }

            testTask "option none stays a single Report" {
                let! value =
                    eff {
                        let! _ = None
                        return 1
                    }
                    |> Eff.runTask ()

                let err: exn = Exit.err value

                Expect.equal (err.GetType()) typeof<Report> "option none should surface as Report"

                match err with
                | Report.ReportAs (wrapped: Option<int>) ->
                    Expect.equal wrapped None "report should preserve the original option payload"
                | _ ->
                    failtest "expected Report carrying None"
            }

            testTask "existing exceptions are wrapped once and preserved as inner exceptions" {
                let boom = exn "boom"

                let! value =
                    eff {
                        let! _ = Eff.err boom
                        return 1
                    }
                    |> Eff.runTask ()

                let err: exn = Exit.err value

                Expect.equal (err.GetType()) typeof<Report> "plain exceptions should normalize to Report"
                Expect.equal err.Message "boom" "report message should match the original exception"
                Expect.isTrue (obj.ReferenceEquals(err.InnerException, boom)) "original exception should be preserved as InnerException"
            }

            testTask "existing reports are not rewrapped" {
                let boom = Report.make "boom"

                let! value =
                    eff {
                        let! _ = Eff.err boom
                        return 1
                    }
                    |> Eff.runTask ()

                let err: exn = Exit.err value

                Expect.isTrue (obj.ReferenceEquals(err, boom)) "existing reports should flow through unchanged"
            }
        ]
