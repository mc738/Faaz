// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open Faaz
open Faaz.Common

// Define a function to construct a message to print
let from whom =
    sprintf "from %s" whom

[<EntryPoint>]
let main argv =
    let fsi = ScriptHost.fsiSession ()
    
    match ScriptHost.eval<int> "C:\\Users\\44748\\Projects\\Faaz\\Scripts\\BuildScripts.fsx" "BuildScripts.FDOM.run" fsi with
    | Ok _ -> 0
    | Error e ->
        printfn $"Error: {e}"
        -1