// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.IO
open FStore.S3
open Faaz
open Faaz.ToolKit.Data
open Faaz.ToolKit.Dev

[<EntryPoint>]
let main argv =
    let fsi = ScriptHost.fsiSession ()
    let bucket = "builds"
    
    match S3Context.Create("C:\\ProjectData\\s3_test\\s3_config-builds.json") with
    | Ok s3 ->
        let major = 1
        let minor = 1
        let revision = 0
        let command = $"BuildScripts.TestRepo.run {major} {minor} {revision}"
        match ScriptHost.eval<int> "C:\\Users\\44748\\Projects\\Faaz\\Scripts\\BuildScripts.fsx" command fsi with
        | Ok _ -> 0
        | Error e ->
            printfn $"Error running build script: {e}"
            -1
    | Error e ->
        printfn $"Error loading s3 context: {e}"
        -2