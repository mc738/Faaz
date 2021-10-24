namespace Faaz.ToolKit.Dev

open System
open System.IO
open Faaz

[<RequireQualifiedAccess>]
module DotNet =
    
    let getPublishPath (sc: ScriptContext) = sc.GetValue("publish-dir", "")

    let getTestsPath (sc: ScriptContext) = sc.GetValue("tests-dir", "")
        
    let getVersion (sc: ScriptContext) = sc.GetValue("version", "")
    
    let getVersionSuffix (sc: ScriptContext) = sc.TryGetValue("version-suffix")
    
    let getBuildName (sc: ScriptContext) = sc.GetValue("build-name", "")
    
    let getSrcPath (sc: ScriptContext) = sc.TryGetValue("src-path")
    
    let createSourcePath (name: string) (sc: ScriptContext) = Path.Combine(getSrcPath sc |> Option.defaultValue "", name)
    
    
    
    let publish (context: ScriptContext) (dotnetPath: string) name =
        
        let args =
            [ "publish"
              createSourcePath name context
              "--configuration Release"
              $"--output {Path.Combine(getPublishPath context, name)}"
              "-p:UseAppHost=false"
              $"/p:VersionPrefix={getVersion context}"
              match getVersionSuffix context with
              | Some v -> 
                  $"/p:VersionSuffix={v}"
              | None -> ""
              $"/p:InformationalVersion={getBuildName context}" ]
            |> (fun a -> String.Join(' ', a))
        //let args =
        
        //    "publish --configuration Release --output {output} /p:VersionPrefix={}.{}.{}"

        let output, errors =
            Process.execute dotnetPath args  None//(getSrcPath context)

        match errors.Length = 0 with
        | true -> Ok output.Head
        | false -> Error(String.Join(Environment.NewLine, errors))

    /// Run dotnet test and return the past to the results file.
    let test (context: ScriptContext) dotnetPath testName =
        context.Log("dot-net-test", "Running tests.")
        // dotnet test --logger "trx;logfilename=mytests.xml" -r C:\TestResults\FDOM\
        let args =
            [ "test"
              createSourcePath testName context
              $"--logger \"trx;logfilename={testName}.xml\""
              $"-r {getTestsPath context}" ]
            |> (fun a -> String.Join(' ', a))
        //let args =
        //    "publish --configuration Release --output {output} /p:VersionPrefix={}.{}.{}"

        let output, errors =
            Process.execute dotnetPath args None//path

        match errors.Length = 0 with
        | true ->
            output |> List.map (fun o -> context.Log("dot-net-test", o)) |> ignore
            context.Log("dot-net-test", "Tests complete.")
            Ok (Path.Combine(getTestsPath context, $"{testName}.xml"))
        | false ->
            errors |> List.map (fun e -> context.LogError("dot-net-test", e)) |> ignore
            let errorMessage = String.Join(Environment.NewLine, errors)
            context.LogError("dot-net-test", $"Tests failed. Error: {errorMessage}")            
            Error (String.Join(Environment.NewLine, errors))