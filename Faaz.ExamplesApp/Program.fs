

module FSharpCompiled =
    
    
    
    module HelloWorld =
        
        open Faaz.V1.FSharp.Compiled
        
        let run () =
            let scriptPath = "C:\\Users\\mclif\\Projects\\dotnet\\Faaz\\Faaz.ExamplesApp\\Scripts\\FSharp\\HelloWorld.fsx"
            
            let cfg = ({
                OutputPath = "C:\\Users\\mclif\\Projects\\data\\Faaz\\dlls"
                TargetProfile = "netcore"
                Options = Compiler.CompilerOptions.Default
            } : Compiler.CompilerConfiguration)
            
            let ctx = Compiler.CompilerContext.Create(cfg)
            
            match Compiler.compileScript ctx scriptPath with
            | Ok resultValue ->
                match Loader.loadAssembly resultValue with
                | Ok assembly ->
                    let extractor = Extractor.FunctionExtractor(assembly)
                    
                    match extractor.TryGetFunction<string option, unit>("HelloWorld.run") with
                    | Ok resultValue ->
                        resultValue (Some "Max")
                        resultValue None
                        //resultValue "Max"
                    | Error errorValue -> failwith "todo"
                | Error errorValue -> failwith "todo"
            | Error errorValue -> failwith "todo"
            
            
            ()
        
        
    
    
    
    ()

FSharpCompiled.HelloWorld.run ()