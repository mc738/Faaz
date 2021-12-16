open Faaz.ToolKit.Data

#r "C:\\Users\\44748\\Projects\\Faaz\\Faaz\\bin\\Debug\\net5.0\\Faaz.dll"
#r "C:\\Users\\44748\\Projects\\Faaz\\Faaz.ToolKit\\bin\\Debug\\net5.0\\Faaz.ToolKit.dll"
#r "C:\\Users\\44748\\Projects\\FStore\\FStore\\bin\\Debug\\net5.0\\FStore.dll"

open Faaz
open Faaz.Pipelines
open Faaz.ToolKit.Dev
open FStore

module FDOM =
    let config =
        ({ DotNetPath = "C:\\Program Files\\dotnet\\dotnet"
           GitPath = "git"
           SourceUrl = "https://github.com/mc738/FDOM.git"
           Name = "FDOM"
           Tests = [
               "FDOM.UnitTests"
               "FDOM.IntegrationTests"
           ]
           Publishes = [
               "FDOM.Core"
               "FDOM.Rendering.Html"
               "FDOM.Rendering.Razor"
           ]
           OutputDirectory = "C:\\Builds"
           Args = [
               { Key = "s3-config-path"; Value = "C:\\ProjectData\\s3_test\\s3_config-builds.json" }
               { Key = "s3-config-bucket"; Value = "builds" }               
           ] }: BuildPipeline.Configuration)

    let version =
        ({ Major = 0
           Minor = 1
           Revision = 0
           Suffix = None }: BuildPipeline.Version)
        
    let buildPipeline (config: BuildPipeline.Configuration) (version: BuildPipeline.Version) =
        let pipeline =
            fun _ -> BuildPipeline.initialize config version
            >=> BuildPipeline.test config
            >=> BuildPipeline.publish config
            >=> BuildPipeline.createZip
            >=> BuildPipeline.uploadBuildArtifact
            >=> BuildPipeline.cleanUp
            >=> (fun bc -> BuildPipeline.getBuildArtifactName bc |> Ok) 
                
        match pipeline () with
        | Ok r -> 0
        | Error e -> -1
    let run () =
        printfn "*** Running from script ***"
        buildPipeline config version
        
        
module UploadTest =
    
    let run _ =
        match S3.loadContext("C:\\ProjectData\\s3_test\\s3_config-builds.json") with
        | Ok s3 ->
            match attempt (fun _ -> s3.UploadObject("builds", "test-pdf", "C:\\ProjectData\\s3_test\\test.pdf")  |> Async.RunSynchronously) with
            | Ok _ -> "File uploaded"
            | Error e -> $"Error: {e}"
        | Error e -> $"Error loading context: {e}"
        