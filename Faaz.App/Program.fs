// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open Faaz
open Faaz.Common
open Faaz.ScriptHost
open Fipc.Core
open Fipc.Core.Common
open Microsoft.FSharp.Control

// Define a function to construct a message to print
let from whom = sprintf "from %s" whom

module Examples =

    /// An example listener, this will simply print the messages it receives.
    let listener (reader: FipcConnectionReader) =
        let rec testLoop () =
            match reader.TryReadMessage() with
            | Some msg ->
                match msg.Body with
                | FipcMessageContent.Text t -> printfn $"Message: {t}"
                | _ -> printfn $"Message type not supported yet."
            | None -> () //printfn $"No messages."

            Async.Sleep 1000 |> Async.RunSynchronously
            testLoop ()

        printfn $"Starting example listener loop."
        testLoop ()
        ()


    let runInBackground (fn) v = async { return fn v } |> Async.Start


[<EntryPoint>]
let main argv =

    let pipeName = "faaz-test-1"

    // Create stream out (for agent).
    //let config = Messaging.createConfiguration "test-faaz" pipeName
    //let streamServer = Server.startStreamServer config
    let reader = Messaging.createServer "server" pipeName
    
    let fsi = ScriptHost.fsiSession ()

    let ctx = ({ FsiSession = fsi }: HostContext)

    
    
    //let agent = HostAgent(ctx)

    //agent.RunScript(
    //    "C:\\Users\\44748\\Projects\\Faaz\\Faaz.TestScripts\\TestScript.fsx",
    //    $"TestScript.test \"{pipeName}\""
    //)

    // Start the agent.

    //let reader = Messaging.createServer "" ""

    // Read output from agent.
    async { return Examples.listener reader }
    |> Async.Start

    printfn "*** Back to main"
   
    let rec loop () =
        Async.Sleep 10000 |> Async.RunSynchronously
        loop ()
    
    0
// Start the listener


//match S3Context.Create("")

//match ScriptHost.eval<int> "C:\\Users\\44748\\Projects\\Faaz\\Scripts\\BuildScripts.fsx" "BuildScripts.FDOM.run" fsi with
//| Ok _ ->

//    0
//| Error e ->
//    printfn $"Error: {e}"
//    -1
