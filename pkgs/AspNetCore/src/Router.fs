namespace EffSharp.AspNetCore

open EffSharp.Core
open Microsoft.AspNetCore.Http

type HttpHandler<'env> = HttpContext -> Eff<Response, Response, 'env>
type HttpMiddleware<'env> = HttpHandler<'env> -> HttpHandler<'env>

[<Struct>]
type Method =
  private
  | Method of string

  member this.Value =
    let (Method value) = this
    value

type Route<'env> = internal {
  Method: Method
  Pattern: string
  Handler: HttpHandler<'env>
}

type Router<'env> = internal {
  RoutesRev: Route<'env> list
  LayersRev: HttpMiddleware<'env> list
}

module Router =
  let private method value = Method value

  let private combine prefix pattern =
    let trimEnd (value: string) = value.TrimEnd('/')
    let trimStart (value: string) = value.TrimStart('/')

    match trimEnd prefix, trimStart pattern with
    | "", "" -> "/"
    | "", right -> "/" + right
    | left, "" -> left
    | left, right -> left + "/" + right

  let private applyLayers layersRev handler =
    layersRev |> List.fold (fun next middleware -> middleware next) handler

  let private materializeRoutes router =
    router.RoutesRev
    |> List.rev
    |> List.map (fun route -> {
      route with
          Handler = applyLayers router.LayersRev route.Handler
    })

  let empty: Router<'env> = {
    RoutesRev = []
    LayersRev = []
  }

  let layer middleware router = {
    router with
        LayersRev = middleware :: router.LayersRev
  }

  let route method pattern handler router = {
    router with
        RoutesRev =
          {
            Method = method
            Pattern = pattern
            Handler = handler
          }
          :: router.RoutesRev
  }

  let get pattern handler router =
    route (method HttpMethods.Get) pattern handler router

  let post pattern handler router =
    route (method HttpMethods.Post) pattern handler router

  let put pattern handler router =
    route (method HttpMethods.Put) pattern handler router

  let patch pattern handler router =
    route (method HttpMethods.Patch) pattern handler router

  let delete pattern handler router =
    route (method HttpMethods.Delete) pattern handler router

  let nest prefix nested router =
    let nestedRoutes =
      materializeRoutes nested
      |> List.map (fun route -> {
        route with
            Pattern = combine prefix route.Pattern
      })

    {
      router with
          RoutesRev = List.rev nestedRoutes @ router.RoutesRev
    }

  let merge other router =
    let otherRoutes = materializeRoutes other

    {
      router with
          RoutesRev = List.rev otherRoutes @ router.RoutesRev
    }

  let internal routes router = materializeRoutes router
