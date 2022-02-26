namespace Faaz

open Faaz.ScriptHost
open Fipc.Core.Common

[<RequireQualifiedAccess>]
module Agent =

    type Script = { Path: string; Name: string }

    type Request =
        | RunScript of Script
        | ShutDown
        | Restart
        | Flush of AsyncReplyChannel<unit>


    let start (ctx: HostContext) =
        MailboxProcessor<Request>.Start
            (fun inbox ->

                let rec loop () =
                    async {
                        let! request = inbox.Receive()

                        let run =
                            match request with
                            | RunScript script ->
                                match ctx.Eval<int>(script.Path, script.Name) with
                                | Ok _ -> printfn "*** AGENT: Script complete."
                                | Error e -> printfn $"*** AGENT: Error! {e}"
                                true
                            | ShutDown -> false
                            | Restart -> false
                            | Flush rc ->
                                rc.Reply()
                                true

                        if run then return! loop ()
                    }
                loop ())

type HostAgent(ctx: HostContext) =

    let agent = Agent.start ctx

    member _.RunScript(path, name) =
        agent.Post(Agent.Request.RunScript({ Path = path; Name = name }: Agent.Script))