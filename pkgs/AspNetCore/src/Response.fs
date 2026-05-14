namespace EffSharp.AspNetCore

open System
open System.Text
open System.Text.Json
open EffSharp.Core
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives

type Response =
  {
    mutable StatusCode: int
    mutable ContentType: string voption
    Headers: ResizeArray<string * string>
    mutable Body: byte array voption
  }

module internal Response =
  let write (ctx: HttpContext) (response: Response) : Eff<unit, Response, 'env> =
    if ctx.Response.HasStarted then
      Pure()
    else
      Eff.ofTask
      <| fun () -> task {
        ctx.Response.StatusCode <- response.StatusCode

        match response.ContentType with
        | ValueSome contentType -> ctx.Response.ContentType <- contentType
        | ValueNone -> ()

        for name, value in response.Headers do
          ctx.Response.Headers[name] <- StringValues(value)

        match response.Body with
        | ValueSome body when body.Length > 0 ->
            do! ctx.Response.Body.WriteAsync(body, 0, body.Length)
        | ValueSome _
        | ValueNone ->
            ()
      }

module Res =
  let private jsonOptions =
    JsonSerializerOptions(JsonSerializerDefaults.Web)

  let empty () : Response =
    {
      StatusCode = StatusCodes.Status200OK
      ContentType = ValueNone
      Headers = ResizeArray()
      Body = ValueNone
    }

  let bytes (body: byte array) : Response =
    {
      StatusCode = StatusCodes.Status200OK
      ContentType = ValueNone
      Headers = ResizeArray()
      Body = ValueSome body
    }

  let text (body: string) : Response =
    {
      StatusCode = StatusCodes.Status200OK
      ContentType = ValueSome "text/plain; charset=utf-8"
      Headers = ResizeArray()
      Body = ValueSome(Encoding.UTF8.GetBytes body)
    }

  let json (body: 't) : Response =
    {
      StatusCode = StatusCodes.Status200OK
      ContentType = ValueSome "application/json; charset=utf-8"
      Headers = ResizeArray()
      Body = ValueSome(JsonSerializer.SerializeToUtf8Bytes(body, jsonOptions))
    }

  let status code (response: Response) =
    response.StatusCode <- code
    response

  let contentType contentType (response: Response) =
    response.ContentType <- ValueSome contentType
    response

  let header name value (response: Response) =
    response.Headers.Add(name, value)
    response

  let badRequest message =
    text message |> status StatusCodes.Status400BadRequest

  let notFound message =
    text message |> status StatusCodes.Status404NotFound

  let internalServerError (ex: exn) =
    text ex.Message |> status StatusCodes.Status500InternalServerError

  let chunk (body: byte array) (ctx: HttpContext) : Eff<unit, Response, 'env> =
    Eff.ofTask
    <| fun () -> task {
      if not ctx.Response.HasStarted then
        ctx.Response.StatusCode <- StatusCodes.Status200OK

      do! ctx.Response.Body.WriteAsync(body, 0, body.Length)
      do! ctx.Response.Body.FlushAsync()
    }

  let sse (data: string) (ctx: HttpContext) : Eff<unit, Response, 'env> =
    Eff.ofTask
    <| fun () -> task {
      if not ctx.Response.HasStarted then
        ctx.Response.StatusCode <- StatusCodes.Status200OK
        ctx.Response.ContentType <- "text/event-stream"
        ctx.Response.Headers["Cache-Control"] <- StringValues("no-cache")

      do! ctx.Response.WriteAsync($"data: {data}\n\n")
      do! ctx.Response.Body.FlushAsync()
    }
