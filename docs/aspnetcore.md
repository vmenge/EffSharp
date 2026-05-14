# EffSharp.AspNetCore Plan

## Goal

`EffSharp.AspNetCore` provides an Axum-like router for ASP.NET Core where route
handlers are EffSharp effects.

The package should keep ASP.NET Core as the HTTP server and endpoint host, while
EffSharp owns the handler execution model.

Core constraints:

- no ASP.NET Core dependency injection inside handlers
- no per-request Eff environment
- no handler reflection or automatic argument binding
- no router-level error mapper
- no response conversion interface
- request values are read explicitly from `HttpContext`
- app dependencies are supplied once when the router is mounted
- route handlers return HTTP responses on both success and failure

## Public Shape

```fsharp
type HttpHandler<'env> =
  HttpContext -> Eff<Response, Response, 'env>
```

A handler receives the ASP.NET `HttpContext` as an explicit argument and returns
an Eff that either succeeds with a `Response` or fails with a `Response`.

```fsharp
let getUser (ctx: HttpContext) : Eff<Response, Response, #Effect.Users> = eff {
  match Req.pathParam<int> "id" ctx with
  | None ->
      return! Err(Res.text "invalid user id" |> Res.status 400)

  | Some id ->
      let! user =
        Users.find id
        |> Eff.mapErr UserError.toResponse

      return Res.json (UserDto.from user)
}
```

Domain errors are mapped once at the HTTP boundary, normally around a whole
domain workflow, not at every operation.

Middleware is a handler wrapper.

```fsharp
type HttpMiddleware<'env> =
  HttpHandler<'env> -> HttpHandler<'env>
```

Middleware is general request handling code. Authentication and authorization are
not special package concepts; applications define those decisions explicitly as
middleware.

## Router

The router is dependency-free route structure.

```fsharp
let router =
  Router.empty
  |> Router.get "/users/{id}" Users.getUser
  |> Router.post "/users" Users.createUser
  |> Router.get "/health" Health.get
```

The dependency graph is supplied once when mounted into ASP.NET Core.

```fsharp
app.UseEffRouter(router, deps)
```

`deps` is the normal EffSharp environment value. It can satisfy any generated
effect interfaces required by registered handlers.

```fsharp
type AppEnv(users: Users, clock: Clock) =
  interface Effect.Users with
    member _.Users = users

  interface EffSharp.Std.Effect.Clock with
    member _.Clock = clock
```

No request data is placed in the Eff environment. Request data comes from
`HttpContext`.

## Router API

Initial router surface:

```fsharp
module Router =
  val empty : Router<'env>

  val layer :
    HttpMiddleware<'env> -> Router<'env> -> Router<'env>

  val get :
    string -> HttpHandler<'env> -> Router<'env> -> Router<'env>

  val post :
    string -> HttpHandler<'env> -> Router<'env> -> Router<'env>

  val put :
    string -> HttpHandler<'env> -> Router<'env> -> Router<'env>

  val patch :
    string -> HttpHandler<'env> -> Router<'env> -> Router<'env>

  val delete :
    string -> HttpHandler<'env> -> Router<'env> -> Router<'env>

  val nest :
    string -> Router<'env> -> Router<'env> -> Router<'env>

  val merge :
    Router<'env> -> Router<'env> -> Router<'env>
```

`Router.layer` applies middleware to the whole router value as a group. Multiple
layers run top-to-bottom in declaration order.

```fsharp
let requireAuthenticated next ctx = eff {
  if isAuthenticated ctx then
    return! next ctx
  else
    return! Err(Res.text "unauthorized" |> Res.status 401)
}

let privateRoutes =
  Router.empty
  |> Router.layer requireAuthenticated
  |> Router.get "/me" Me.get
  |> Router.get "/admin" Admin.get
```

Router composition keeps middleware scoped to the router it was applied to.

```fsharp
let router =
  Router.empty
  |> Router.merge publicRoutes
  |> Router.merge privateRoutes
```

ASP.NET Core adapter:

```fsharp
type IEndpointRouteBuilder with
  member UseEffRouter : Router<'env> * 'env -> IEndpointRouteBuilder
```

The adapter registers each route with ASP.NET Core endpoint routing. ASP.NET Core
matches routes. EffSharp runs handlers.

Runtime flow:

```fsharp
HttpContext
  -> selected route handler
  -> handler ctx
  -> Eff.run deps
  -> write returned Response
```

`Exit.Ok response` and `Exit.Err response` are both written as HTTP responses.
`Exit.Exn ex` is written as an internal server error response.
`Exit.Aborted` is written as an aborted/cancelled response when possible.
If `ctx.Response.HasStarted` is true after the handler runs, the adapter skips
writing the returned `Response`.

## Request Helpers

`Req` exposes explicit helpers over `HttpContext`.

Path/query/header helpers should be synchronous and return options.

```fsharp
module Req =
  val pathParam<'t> : string -> HttpContext -> 't option
  val queryParam<'t> : string -> HttpContext -> 't option
  val header : string -> HttpContext -> string option
```

Body helpers perform I/O and return Eff.

```fsharp
module Req =
  val json<'t> : HttpContext -> Eff<'t, Response, 'env>
  val form<'t> : HttpContext -> Eff<'t, Response, 'env>
```

Malformed request bodies fail with `Response`, usually `400 Bad Request`.

## Response

`Response` is a concrete mutable value. Modifiers mutate and return the same
response instance to avoid record-copy chains and wrapper functions.

Sketch:

```fsharp
type Response =
  {
    mutable StatusCode: int
    mutable ContentType: string voption
    Headers: ResizeArray<string * string>
    mutable Body: byte[] voption
  }
```

Rules:

- `Response` is single-use request state.
- `Res.*` constructors return fresh instances.
- `Res.status`, `Res.header`, and similar modifiers mutate and return the same
  instance.

## Response Helpers

```fsharp
module Res =
  val empty : unit -> Response

  val text : string -> Response
  val json : 't -> Response
  val bytes : byte[] -> Response

  val status : int -> Response -> Response
  val header : string -> string -> Response -> Response
  val contentType : string -> Response -> Response

  val badRequest : string -> Response
  val notFound : string -> Response
  val internalServerError : exn -> Response
```

Example:

```fsharp
Res.text "whatever"
|> Res.status 222
|> Res.header "x-test" "abc"
```

Advanced response helpers can write directly to `HttpContext`.

```fsharp
module Res =
  val chunk :
    byte[] ->
    HttpContext ->
      Eff<unit, Response, 'env>

  val sse :
    string ->
    HttpContext ->
      Eff<unit, Response, 'env>
```

Streaming helpers initialize the ASP.NET response on first write when needed.
Handlers return any normal response value. The adapter skips writing it when the
ASP.NET response has already started.

```fsharp
let events ctx = eff {
  do! Res.sse "started" ctx
  do! Res.sse "done" ctx

  return Res.empty ()
}
```

## Implementation Plan

1. Add `pkgs/AspNetCore/src/AspNetCore.fsproj`.
2. Reference `EffSharp.Core`.
3. Add ASP.NET Core framework reference.
4. Define `Response` and `Res`.
5. Define `Req`.
6. Define `HttpHandler<'env>`.
7. Define `Router<'env>` as immutable route structure.
8. Add ASP.NET Core extension `UseEffRouter`.
9. Add tests with an in-memory ASP.NET Core test host.
10. Add examples showing request params, JSON body parsing, domain error mapping,
    and streaming through direct `HttpContext` helpers.

## Feasibility Notes

The router can preserve compile-time Eff environment constraints.

Handlers requiring different generated effect interfaces can compose into one
router, and the final environment value supplied to `UseEffRouter` must satisfy
those constraints.

This keeps dependency checking compile-time while avoiding runtime DI lookup in
handlers.
