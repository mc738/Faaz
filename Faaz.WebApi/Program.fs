open System
open System.IO
open System.Security
open System.Text.Json
open System.Text.Json.Serialization
open Faaz
open Faaz.ScriptHost
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Diagnostics.HealthChecks
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.HttpOverrides
open Giraffe
open Microsoft.Extensions.Logging
open Peeps
open Peeps.Extensions
open Peeps.Logger
open Peeps.Store
open Peeps.Extensions
open Peeps.Monitoring.Extensions
open Faaz.WebApi

[<CLIMutable>]
type AppConfiguration = {
    [<JsonPropertyName("connectionString")>]
    ConnectionString: string
    [<JsonPropertyName("scriptsDirectory")>]
    ScriptsDirectory: string
}

let forwardingHeaderOptions =
    // Forwarding header options for nginx.
    // https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-5.0
    let options = ForwardedHeadersOptions()

    options.ForwardedHeaders <-
        ForwardedHeaders.XForwardedFor
        ||| ForwardedHeaders.XForwardedProto

    options

let configureApp (app: IApplicationBuilder) =
    app.UseDeveloperExceptionPage() |> ignore
    
    app
        .UseForwardedHeaders(forwardingHeaderOptions)
        .UsePeepsMonitor()
        .UseRouting()
        .UseCors(fun (b: CorsPolicyBuilder) ->
            b
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowAnyOrigin()
            |> ignore)
        
        .UsePeepsHealthChecks()
        //.UseAuthentication()
        //.UseFAuth(context)
        .UseGiraffe App.routes

let configureServices (logStore: LogStore) (ctx: HostContext) (*(buildAgent: BuildAgent)*) (*(securityContext: SecurityContext)*) (*(jwt: Tokens.JwtSettings)*) (services: IServiceCollection) =
    // TODO add comms support.
    //services.AddHttpClient<CommsClient>() |> ignore

    services
        .AddPeepsLogStore(logStore)
        .AddPeepsMonitorAgent(logStore.Path)
        .AddPeepsRateLimiting(100)
        .AddSingleton<HostContext>(fun _ -> ctx)
        .AddSingleton<HostAgent>()
        //.AddSingleton<BuildAgent>(fun _ -> buildAgent)
        //.AddSingleton<Tokens.JwtSettings>(fun _ -> jwt)
        //.AddSingleton<FAuthContext>(context)
        //.AddScoped<MySqlContext>(fun _ -> MySqlContext.Connect(securityContext.ConnectionString))
        //.AddScoped<OrganisationRepository>()
        //.AddScoped<CategoryRepository>()
        //.AddScoped<ResourceRepository>()
        //.AddScoped<KeywordRepository>()
        //.AddScoped<ServiceRepository>()
        //.AddScoped<AuthHandler>()
        .AddCors()
        .AddGiraffe()
        //.AddAuth(jwt)
        |> ignore

// TODO Add health check support.

    services.AddHealthChecks()
            .AddPeepsHealthChecks(5000000L, 1000, logStore.StartedOn)
            |> ignore
    (*        .AddCheck<KInkBMSHealthCheck>(
                "Database-check",
                HealthStatus.Unhealthy,
                [| "database" |])
            .AddCheck<FlingItemHealthCheck>(
                "Mail-check",
                HealthStatus.Degraded,
                [| "mail" |])|> ignore
    *)

let configureLogging (peepsCtx: PeepsContext) (logging: ILoggingBuilder) =
    logging.ClearProviders().AddPeeps(peepsCtx)
    |> ignore


[<EntryPoint>]
let main argv =

    let path =
        match argv.Length > 0 with
        | true -> argv.[0]
        | false -> ""

    let runId = Guid.NewGuid()
    let startedOn = DateTime.UtcNow
    
    let logStore = LogStore(Path.Combine(path, "logs"), "Faaz-WebApi", runId, startedOn)
    let logActions = [
        Actions.writeToConsole
        Actions.writeToStore logStore
    ]
    
    //let appConfig = File.ReadAllText (Path.Combine(path, "config.json"))  |> JsonSerializer.Deserialize<AppConfiguration>
    //let jwt = File.ReadAllText (Path.Combine(path, "jwt.json")) |> JsonSerializer.Deserialize<Tokens.JwtSettings>
    
    let peepsCtx =
        PeepsContext.Create(Path.Combine(path, "logs"), "Faaz-WebApi", logActions)

        
    //let context = MySqlContext.Connect(appConfig.ConnectionString)    
    printfn "Starting fsi"
    let fsi = ScriptHost.fsiSession ()
    printfn "Complete."    
    let hostContext =  ({ FsiSession = fsi } : HostContext)
    //let starting
    //let buildAgent = BuildAgent(hostContext, context, appConfig.ScriptsDirectory)
    //let agent = HostAgent(hostContext)
    
    
    Host
        .CreateDefaultBuilder()
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .UseKestrel()
                .UseUrls("http://0.0.0.0:21115;https://0.0.0.0:21116;")
                .Configure(configureApp)
                .ConfigureServices(configureServices logStore hostContext)
                .ConfigureLogging(configureLogging peepsCtx)
            |> ignore)
        .Build()
        .Run()
        
    0 // return an integer exit code


(*
open System
open Faaz
open Faaz.ScriptHost
open Faaz.ToolKit.Dev
open Forge.Core.Agents
open Freql.MySql

printfn "Starting fsi"
let fsi = ScriptHost.fsiSession ()
printfn "Complete."

let hostContext =  ({ FsiSession = fsi } : HostContext)

let context = MySqlContext.Connect("Server=localhost;Database=forge;Uid=max;Pwd=letmein;")

let buildAgent = BuildAgent(hostContext, context, "C:\\Users\\44748\\Projects\\Forge\\Scripts\\BuildScripts.fsx")

buildAgent.StartMinor("TestRepo")

Async.Sleep 100000 |> Async.RunSynchronously

// For more information see https://aka.ms/fsharp-console-apps
printfn "Hello from F#"*)