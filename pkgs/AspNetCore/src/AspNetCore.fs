namespace EffSharp.AspNetCore

open EffSharp.Core
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing

module internal Adapter =
  let private writeExit ctx exit =
    match exit with
    | Exit.Ok response
    | Exit.Err response ->
        Response.write ctx response

    | Exit.Exn ex ->
        Response.write ctx (Res.internalServerError ex)

    | Exit.Aborted ->
        Response.write
          ctx
          (Res.text "request aborted" |> Res.status StatusCodes.Status499ClientClosedRequest)

  let endpoint env handler =
    RequestDelegate(fun ctx ->
      task {
        let! exit = handler ctx |> Eff.run env
        let! writeExit = writeExit ctx exit |> Eff.run ()

        match writeExit with
        | Exit.Ok () -> ()
        | Exit.Err response ->
            let! _ = Response.write ctx response |> Eff.run ()
            ()
        | Exit.Exn ex ->
            let! _ = Response.write ctx (Res.internalServerError ex) |> Eff.run ()
            ()
        | Exit.Aborted ->
            ()
      })

[<AutoOpen>]
module EndpointRouteBuilderExtensions =
  type IEndpointRouteBuilder with
    member this.UseEffRouter(router: Router<'env>, env: 'env) =
      for route in Router.routes router do
        this.MapMethods(route.Pattern, [| route.Method.Value |], Adapter.endpoint env route.Handler)
        |> ignore

      this
