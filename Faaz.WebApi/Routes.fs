namespace Faaz.WebApi

open System
open Faaz
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Giraffe
open Peeps.Monitoring

module Routes =

    [<AutoOpen>]
    module private Utils =
        let errorHandler (logger: ILogger) name code message =
            logger.LogError("Error '{code}' in route '{name}', message: '{message};.", code, name, message)
            setStatusCode code >=> text message

        //let authorize: (HttpFunc -> HttpContext -> HttpFuncResult) =
        //    requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme)

        let getClaim (ctx: HttpContext) (name: string) = ctx.User.FindFirst(name).Value

        let getUserRef (ctx: HttpContext) =
            match Guid.TryParse(getClaim ctx "userRef") with
            | true, ref -> Some(ref)
            | false, _ -> None

        let handleProcResult log name (result: Result<string, string>) next ctx =
            match result with
            | Ok m -> text m next ctx
            | Error e -> errorHandler log name 500 e earlyReturn ctx
            
    let test : HttpHandler =
        let name = "get-deployments-for-location"

        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                //let! request =
                //    ctx.BindJsonAsync<NewSpecific>()
                //    |> Async.AwaitTask

                let log = ctx.GetLogger(name)

                log.LogInformation($"Get deployments for location `{id}` request received.")
                //let dsCtx = ctx.GetService<MySqlContext>()

                return! text "Hello, World!" next ctx
            }
            //|> Async.RunSynchronously
    
    let run : HttpHandler =
        let name = "get-deployments-for-location"

        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                //let! request =
                //    ctx.BindJsonAsync<NewSpecific>()
                //    |> Async.AwaitTask

                let log = ctx.GetLogger(name)

                let agent = ctx.GetService<HostAgent>()
                
                let pipeName = "faaz-logger"
                log.LogInformation($"Running script.")
                                
                agent.RunScript(
                    "C:\\Users\\44748\\Projects\\Faaz\\Faaz.TestScripts\\TestScript.fsx",
                    $"TestScript.test \"{pipeName}\""
                )
                
                //let dsCtx = ctx.GetService<MySqlContext>()

                return! text "Hello, World!" next ctx
            }
    
    let all: (HttpFunc -> HttpContext -> HttpFuncResult) list =
        [ GET (*Routes.Utils.authorize >=>*)
          >=> choose [ route "/test" >=> test ]
          POST
          >=> choose [ route "/run" >=> run ] ]

module App =

    let routes: (HttpFunc -> HttpContext -> HttpFuncResult) =
        let routes =
            List.concat [ Routes.all
                          PeepsMetricRoutes.routes ]

        choose routes

