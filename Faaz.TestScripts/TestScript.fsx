#r "../Faaz/bin/Debug/net6.0/Faaz.dll"
#r "../Faaz/bin/Debug/net6.0/Fipc.Core.dll"
//#r ""

open Faaz.ScriptingContext
open Fipc.Core.Common

let test pipeName =
    try
        //let pipeName = "faaz-test"
        use pipe = connect pipeName
        pipe.TryPostMessage(FipcMessage.StringMessage("Hello, World!")) |> ignore
        pipe.TryPostMessage(FipcMessage.StringMessage("This is a message from a script!")) |> ignore
        pipe.TryPostMessage(FipcMessage.StringMessage($"The pipe name is `{pipeName}`")) |> ignore
        pipe.TryPostMessage(FipcMessage.StringMessage("Waiting a second")) |> ignore
        Async.Sleep 1000 |> Async.RunSynchronously
        pipe.TryPostMessage(FipcMessage.StringMessage("Good night!")) |> ignore
        0
    with
    | exn ->
        printfn $"Error {exn.Message}"
        -1