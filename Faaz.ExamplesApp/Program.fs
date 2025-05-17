open Microsoft.Extensions.Logging

module FSharpCompiled =



    module HelloWorld =

        open Faaz.V1.FSharp.Compiled

        let run (logger: ILogger) =
            let scriptPath =
                "C:\\Users\\mclif\\Projects\\dotnet\\Faaz\\Faaz.ExamplesApp\\Scripts\\FSharp\\ComplexScript.fsx"

            let cfg =
                ({ OutputPath = "C:\\Users\\mclif\\Projects\\data\\Faaz\\dlls"
                   TargetProfile = "netcore"
                   RecompileOption = Compiler.RecompileOption.OnHashChanged
                   HashConfiguration = Compiler.HashConfiguration.Default
                   Options = Compiler.CompilerOptions.Default }
                : Compiler.CompilerConfiguration)

            let ctx = Compiler.CompilerContext.Create(cfg, logger)

            match Compiler.compileScript ctx scriptPath with
            | Ok resultValue ->
                match Loader.loadAssembly resultValue with
                | Ok assembly ->
                    let extractor = Extractor.FunctionExtractor(assembly)

                    match extractor.TryGetFunction<string option, unit>("ComplexScript.run") with
                    | Ok resultValue ->
                        resultValue (Some "Max")
                        resultValue None
                    //resultValue "Max"
                    | Error errorValue -> failwith "todo"
                | Error errorValue -> failwith "todo"
            | Error errorValue -> failwith "todo"


            ()





    ()

let loggerFactory =
    LoggerFactory.Create(fun builder -> builder.AddConsole().AddFilter("Faaz.ExamplesApp", LogLevel.Trace) |> ignore)

let logger = loggerFactory.CreateLogger("Faaz.ExamplesApp")
logger.LogInformation("Starting examples app")
FSharpCompiled.HelloWorld.run logger
