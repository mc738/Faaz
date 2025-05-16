namespace Faaz

open Faaz.ScriptHost
open Fipc.Core.Common
open Microsoft.Extensions.Logging

[<RequireQualifiedAccess>]
module Agent =

    type Script = { Path: string; Name: string }

    type Request =
        | RunScript of Script
        | ShutDown
        | Restart
        | Flush of AsyncReplyChannel<unit>


    let start (ctx: HostContext) (logger: ILogger) =
        MailboxProcessor<Request>.Start
            (fun inbox ->

                let rec loop () =
                    async {
                        let! request = inbox.Receive()

                        let run =
                            match request with
                            | RunScript script ->
                                match ctx.Eval<int>(script.Path, script.Name) with
                                | Ok _ -> logger.LogInformation("Script complete.")
                                | Error e -> logger.LogInformation($"Error! {e}.")
                                true
                            | ShutDown -> false
                            | Restart -> false
                            | Flush rc ->
                                rc.Reply()
                                true

                        if run then return! loop ()
                    }
                loop ())
            
    let listener (reader: FipcConnectionReader) (logger: ILogger) =
        let rec testLoop () =
            match reader.TryReadMessage() with
            | Some msg ->
                match msg.Body with
                | FipcMessageContent.Text t -> logger.LogInformation $"Message: {t}"
                | _ -> logger.LogError $"Message type not supported yet."
            | None ->
                Async.Sleep 1000 |> Async.RunSynchronously
                () //printfn $"No messages."

            testLoop ()

        logger.LogInformation $"Starting example listener loop."
        testLoop ()
        ()

    let runInBackground (fn) v = async { return fn v } |> Async.Start

type HostAgent(ctx: HostContext, logger: ILogger<HostAgent>) =

    let reader = Messaging.createServer "server" "faaz-logger"
    
    let agent = Agent.start ctx logger

    let _ = async { return Agent.listener reader logger } |> Async.Start
    
    member _.RunScript(path, name) =
        agent.Post(Agent.Request.RunScript({ Path = path; Name = name }: Agent.Script))