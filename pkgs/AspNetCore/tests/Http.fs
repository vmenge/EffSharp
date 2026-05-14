namespace EffSharp.AspNetCore.Tests

open System
open System.Net
open System.Net.Http
open System.Text
open EffSharp.AspNetCore
open EffSharp.Core
open Expecto
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Hosting.Server
open Microsoft.AspNetCore.Hosting.Server.Features
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection

type Greeting =
  abstract Greeting: string

type Env(greeting: string) =
  interface Greeting with
    member _.Greeting = greeting

[<CLIMutable>]
type CreateUser = {
  Name: string
}

type RunningApp(app: WebApplication, client: HttpClient) =
  member _.Client = client

  member _.Stop() = eff {
      client.Dispose()
      do! task { return! app.DisposeAsync().AsTask() }
  }

module private Server =
  let start router env = eff {
    let builder = WebApplication.CreateBuilder([||])

    builder.WebHost.ConfigureKestrel(fun options ->
      options.Listen(IPAddress.Loopback, 0)
    )
    |> ignore

    let app = builder.Build()
    app.UseEffRouter(router, env) |> ignore

    do! task { return! app.StartAsync() }

    let addresses =
      app.Services
        .GetRequiredService<IServer>()
        .Features
        .Get<IServerAddressesFeature>()
        .Addresses

    let client = new HttpClient(BaseAddress = Uri(Seq.head addresses))

    return new RunningApp(app, client)
  }

[<AutoOpen>]
module private Test =
  let testEff name eff =
    testTask name {
      let! exit = Eff.run () eff

      match exit with
      | Exit.Ok () -> ()
      | Exit.Err ex
      | Exit.Exn ex -> raise ex
      | Exit.Aborted -> failwith "test effect aborted"
    }

module Http =
  let tests =
    testList
      "AspNetCore"
      [
        testEff "routes request through Eff handler and writes response metadata" <| eff {
          let handler ctx = eff {
            let id = ctx |> Req.pathParam<int> "id" |> Option.defaultValue -1
            let page = ctx |> Req.queryParam<int> "page" |> Option.defaultValue -1
            let header = ctx |> Req.header "x-test" |> Option.defaultValue "missing"

            let! greeting = Eff.read (fun (env: #Greeting) -> env.Greeting)

            return
              Res.text $"{greeting}:{id}:{page}:{header}"
              |> Res.status 201
              |> Res.header "x-eff" "ok"
          }

          let router =
            Router.empty
            |> Router.get "/items/{id}" handler

          let! running = Server.start router (Env "hello")
          defer (running.Stop())

          let request = new HttpRequestMessage(HttpMethod.Get, "/items/42?page=7")
          request.Headers.Add("x-test", "seen")

          let! response = Eff.tryTask(fun () -> running.Client.SendAsync request)
          let! body = Eff.tryTask(fun () -> response.Content.ReadAsStringAsync())

          Expect.equal (int response.StatusCode) 201 "status should come from response"
          Expect.equal body "hello:42:7:seen" "body should include request data and Eff env data"
          Expect.equal
            (response.Headers.GetValues "x-eff" |> Seq.exactlyOne)
            "ok"
            "headers should be written"
        }

        testEff "writes Eff error response" <| eff {
          let handler _ctx = eff {
            return! Err(Res.notFound "missing")
          }

          let router =
            Router.empty
            |> Router.get "/missing" handler

          let! running =  Server.start router ()
          defer (running.Stop())

          let! response = Eff.tryTask(fun () -> running.Client.GetAsync "/missing")
          let! body = Eff.tryTask(fun () -> response.Content.ReadAsStringAsync())

          Expect.equal (int response.StatusCode) 404 "error response should set status"
          Expect.equal body "missing" "error response should write body"
        }

        testEff "decodes json request body inside Eff" <| eff {
          let handler ctx = eff {
            let! body: CreateUser = Req.json ctx
            let! greeting = Eff.read (fun (env: #Greeting) -> env.Greeting)

            return
              Res.json {| message = $"{greeting}, {body.Name}" |}
              |> Res.status 202
          }

          let router =
            Router.empty
            |> Router.post "/users" handler

          let! running = Server.start router (Env "hello")
          defer (running.Stop())

          let content =
            new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")

          let! response = Eff.tryTask(fun () -> running.Client.PostAsync("/users", content))
          let! body = Eff.tryTask(fun () -> response.Content.ReadAsStringAsync())

          Expect.equal (int response.StatusCode) 202 "json handler should set status"
          Expect.stringContains body "\"message\":\"hello, Ada\"" "json response should include handler output"
        }

        testEff "skips returned response after direct write starts response" <| eff {
          let handler ctx = eff {
            do! Res.chunk (Encoding.UTF8.GetBytes "chunk") ctx
            return Res.text "later"
          }

          let router =
            Router.empty
            |> Router.get "/stream" handler

          let! running = Server.start router ()
          defer (running.Stop())

          let! response = Eff.tryTask(fun () -> running.Client.GetAsync "/stream")
          let! body = Eff.tryTask(fun () -> response.Content.ReadAsStringAsync())

          Expect.equal (int response.StatusCode) 200 "direct chunk response should be successful"
          Expect.equal body "chunk" "adapter should not append returned response after response starts"
        }

        testEff "routes nested routers" <| eff {
          let handler _ctx = eff {
            return Res.text "nested"
          }

          let nested =
            Router.empty
            |> Router.get "/child" handler

          let router =
            Router.empty
            |> Router.nest "/api" nested

          let! running =  Server.start router ()
          defer (running.Stop())

          let! response = Eff.tryTask(fun () -> running.Client.GetAsync "/api/child")
          let! body = Eff.tryTask(fun () -> response.Content.ReadAsStringAsync())

          Expect.equal (int response.StatusCode) 200 "nested route should be found"
          Expect.equal body "nested" "nested route should run handler"
        }

        testEff "applies router middleware in declaration order" <| eff {
          let events = ResizeArray<string>()

          let layer name (next: HttpHandler<unit>) ctx = eff {
            events.Add($"{name}:before")
            let! response = next ctx
            events.Add($"{name}:after")
            return response
          }

          let handler _ctx = eff {
            events.Add("handler")
            return Res.text "ok"
          }

          let router =
            Router.empty
            |> Router.layer (layer "one")
            |> Router.layer (layer "two")
            |> Router.get "/order" handler

          let! running = Server.start router ()
          defer (running.Stop())

          let! response = Eff.tryTask(fun () -> running.Client.GetAsync "/order")
          let! body = Eff.tryTask(fun () -> response.Content.ReadAsStringAsync())

          Expect.equal (int response.StatusCode) 200 "middleware should allow handler"
          Expect.equal body "ok" "handler response should be returned"
          Expect.equal
            (List.ofSeq events)
            [ "one:before"; "two:before"; "handler"; "two:after"; "one:after" ]
            "middleware should run top-to-bottom around handler"
        }

        testEff "middleware can short-circuit before handler" <| eff {
          let events = ResizeArray<string>()

          let deny: HttpMiddleware<unit> = fun _next _ctx -> eff {
            return! Err(Res.text "denied" |> Res.status StatusCodes.Status401Unauthorized)
          }

          let handler _ctx = eff {
            events.Add("handler")
            return Res.text "allowed"
          }

          let router =
            Router.empty
            |> Router.layer deny
            |> Router.get "/private" handler

          let! running = Server.start router ()
          defer (running.Stop())

          let! response = Eff.tryTask(fun () -> running.Client.GetAsync "/private")
          let! body = Eff.tryTask(fun () -> response.Content.ReadAsStringAsync())

          Expect.equal (int response.StatusCode) 401 "middleware response should be written"
          Expect.equal body "denied" "middleware body should be returned"
          Expect.isEmpty events "handler should not run after short-circuit"
        }

        testEff "merged routers keep middleware scoped to their router" <| eff {
          let deny: HttpMiddleware<unit> = fun _next _ctx -> eff {
            return! Err(Res.text "private" |> Res.status StatusCodes.Status401Unauthorized)
          }

          let publicRoutes =
            Router.empty
            |> Router.get "/health" (fun _ctx -> eff { return Res.text "ok" })

          let privateRoutes =
            Router.empty
            |> Router.layer deny
            |> Router.get "/me" (fun _ctx -> eff { return Res.text "me" })

          let router =
            Router.empty
            |> Router.merge publicRoutes
            |> Router.merge privateRoutes

          let! running = Server.start router ()
          defer (running.Stop())

          let! publicResponse = Eff.tryTask(fun () -> running.Client.GetAsync "/health")
          let! publicBody = Eff.tryTask(fun () -> publicResponse.Content.ReadAsStringAsync())
          let! privateResponse = Eff.tryTask(fun () -> running.Client.GetAsync "/me")
          let! privateBody = Eff.tryTask(fun () -> privateResponse.Content.ReadAsStringAsync())

          Expect.equal (int publicResponse.StatusCode) 200 "public route should not use private middleware"
          Expect.equal publicBody "ok" "public handler should run"
          Expect.equal (int privateResponse.StatusCode) 401 "private route should use private middleware"
          Expect.equal privateBody "private" "private middleware should short-circuit"
        }

        testEff "nested router middleware runs inside parent middleware" <| eff {
          let events = ResizeArray<string>()

          let layer name (next: HttpHandler<unit>) ctx = eff {
            events.Add(name)
            return! next ctx
          }

          let handler _ctx = eff {
            events.Add("handler")
            return Res.text "nested"
          }

          let nested =
            Router.empty
            |> Router.layer (layer "child")
            |> Router.get "/child" handler

          let router =
            Router.empty
            |> Router.layer (layer "parent")
            |> Router.nest "/api" nested

          let! running = Server.start router ()
          defer (running.Stop())

          let! response = Eff.tryTask(fun () -> running.Client.GetAsync "/api/child")
          let! body = Eff.tryTask(fun () -> response.Content.ReadAsStringAsync())

          Expect.equal (int response.StatusCode) 200 "nested route should succeed"
          Expect.equal body "nested" "nested handler should run"
          Expect.equal
            (List.ofSeq events)
            [ "parent"; "child"; "handler" ]
            "parent middleware should wrap child middleware"
        }

        testEff "middleware can read Eff environment" <| eff {
          let requireAllowed (next: HttpHandler<Env>) ctx = eff {
            let! greeting = Eff.read (fun (env: Env) -> (env :> Greeting).Greeting)

            if greeting = "allowed" then
              return! next ctx
            else
              return! Err(Res.text "forbidden" |> Res.status StatusCodes.Status403Forbidden)
          }

          let router =
            Router.empty
            |> Router.layer requireAllowed
            |> Router.get "/env" (fun _ctx -> eff { return Res.text "ok" })

          let! running = Server.start router (Env "allowed")
          defer (running.Stop())

          let! response = Eff.tryTask(fun () -> running.Client.GetAsync "/env")
          let! body = Eff.tryTask(fun () -> response.Content.ReadAsStringAsync())

          Expect.equal (int response.StatusCode) 200 "middleware should allow request"
          Expect.equal body "ok" "handler should run after environment-backed middleware"
        }
      ]
