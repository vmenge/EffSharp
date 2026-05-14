namespace EffSharp.AspNetCore

open System
open System.ComponentModel
open System.Globalization
open System.Text.Json
open System.Threading.Tasks
open EffSharp.Core
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives

module Req =
  let private jsonOptions =
    JsonSerializerOptions(JsonSerializerDefaults.Web)

  let private firstString (values: StringValues) : string option =
    if StringValues.IsNullOrEmpty values then
      None
    else
      let value = values.[0]

      value
      |> Option.ofObj
      |> Option.map string
      |> Option.filter (String.IsNullOrWhiteSpace >> not)

  let private convert<'t> (value: string) =
    let targetType = typeof<'t>

    try
      if targetType = typeof<string> then
        Some(unbox<'t> value)
      elif targetType.IsEnum then
        Some(Enum.Parse(targetType, value, ignoreCase = true) |> unbox<'t>)
      else
        let converter = TypeDescriptor.GetConverter(targetType)

        if converter.CanConvertFrom(typeof<string>) then
          converter.ConvertFrom(null, CultureInfo.InvariantCulture, value)
          |> unbox<'t>
          |> Some
        else
          Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)
          |> unbox<'t>
          |> Some
    with _ ->
      None

  let pathParam<'t> name (ctx: HttpContext) =
    match ctx.Request.RouteValues.TryGetValue name with
    | true, value when not (isNull value) ->
        match value with
        | :? 't as typed -> Some typed
        | _ -> convert<'t> (string value)
    | _ ->
        None

  let queryParam<'t> name (ctx: HttpContext) =
    match ctx.Request.Query.TryGetValue name with
    | true, values ->
        match firstString values with
        | Some value -> convert<'t> value
        | None -> None
    | _ ->
        None

  let header name (ctx: HttpContext) =
    match ctx.Request.Headers.TryGetValue name with
    | true, values -> firstString values
    | _ -> None

  let private deserializeJson<'t> (ctx: HttpContext) : Task<'t> = task {
      let! value =
        JsonSerializer.DeserializeAsync(ctx.Request.Body, typeof<'t>, jsonOptions)

      if isNull value then
        return raise (InvalidOperationException "request body must not be empty")
      else
        return unbox<'t> value
  }

  let json<'t, 'env> (ctx: HttpContext) : Eff<'t, Response, 'env> =
    Eff.tryTask(fun () -> deserializeJson<'t> ctx)
    |> Eff.mapErr (fun ex -> Res.badRequest ex.Message)

  let form (ctx: HttpContext) : Eff<IFormCollection, Response, 'env> =
    Eff.ofTask(fun () -> task {
      try
        let! form = ctx.Request.ReadFormAsync()
        return Ok form
      with ex ->
        return Error(Res.badRequest ex.Message)
    })
    |> Eff.bind (fun result -> Eff.ofResult result)
