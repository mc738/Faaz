namespace Faaz.V1.FSharp.Compiled

open System
open System.IO
open System.Security.Cryptography
open System.Text.RegularExpressions
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics

module Compiler =

    type CompilerConfiguration =
        {

            //AllSigns: bool


            /// Compiler option: -a [file name]
            OutputPath: string
            TargetProfile: string
            Options: CompilerOptions
        }

    and CompilerOptions =
        {
            /// Compiler option: --allsigs
            AllSign: bool
        }

        static member Default = { AllSign = false }

    type CompilerContext =
        private
            { Checker: FSharpChecker
              Configuration: CompilerConfiguration }

        static member Create(cfg: CompilerConfiguration) =
            { Checker = FSharpChecker.Create()
              Configuration = cfg }

    type CompilerError =
        | InvalidFileType of Extension: string
        | CompilerFailure of Errors: FSharpDiagnostic array * Exception: exn option

    [<RequireQualifiedAccess>]
    type ScriptDependencyType =
        | Script of string
        | Dll of string
        | Nuget of string

    type HashType =
        | MD5
        | SHA256
        
    [<AutoOpen>]
    module private Internal =

        let loadRegex =
            Regex("#load[ \t]*\"(?<path>(.*?))\"", RegexOptions.Singleline ||| RegexOptions.Compiled)

        let referenceRegex =
            Regex("#r[ \t]*\"(?<path>(.*?))\"", RegexOptions.Singleline ||| RegexOptions.Compiled)

        let nugetRegex =
            Regex("#r[ \t]*\"nuget:[ \t]*(?<path>(.*?))\"", RegexOptions.Singleline ||| RegexOptions.Compiled)

        let makeArgs (ctx: CompilerContext) (scriptPath: string) (outputAssemblyName: string) =
            [| "fsc.exe"
               "-o"
               outputAssemblyName
               "-a"
               scriptPath
               $"--targetprofile:{ctx.Configuration.TargetProfile}" |]

        let makeDllName (scriptName: string) (hash: string) = $"{scriptName}__{hash}.dll"

        let getDependencies (scriptPath: string) =
            File.ReadLines(scriptPath)
            |> Seq.choose (fun line ->
                //match line with
                try
                    match line with
                    | _ when nugetRegex.IsMatch(line) ->
                        //let matches =
                        nugetRegex.Matches(line).[0].Groups["path"].Value
                        |> ScriptDependencyType.Nuget
                        |> Some
                    | _ when referenceRegex.IsMatch(line) ->
                        referenceRegex.Matches(line).[0].Groups["path"].Value
                        |> ScriptDependencyType.Dll
                        |> Some
                    | _ when loadRegex.IsMatch(line) ->
                        loadRegex.Matches(line).[0].Groups["path"].Value
                        |> ScriptDependencyType.Script
                        |> Some
                    | _ -> None
                with _ ->
                    // This is a best-effect attempted to find dependencies.
                    // In the case of an error being thrown simply skip.
                    // This will likely cause a re-compilation,
                    // but that is not an issue.
                    None)
            
        let hashFile (hasher: HashAlgorithm) (path: string) =
            use fs = File.OpenRead path
            
            hasher.ComputeHash(fs)
            |> fun hashBytes -> BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant()
            
            //hasher.ComputeHashAsync
            //
            //use crypto = new CryptoStream(fs, hasher, CryptoStreamMode.Read)
            //
            ////do! File.ReadAllBytesAsync(path) |> hasher.ComputeHashAsync
            //
            //File.ReadAllBytes(path)  
        
        let p (path: string) = Path.IsPathFullyQualified(path)
        
        //let p (path: string) = Path.getf
            
            
        let generateHash (scriptPath: string) =
            
            let hasher = SHA256.Create()
            
            
            let rec traverse (path: string) =
                ()
            
            
            ()

    // NOT supported in the lastest version of FSC
    //let compileScriptToDynamicAssemblyAsync (cfg: CompilerConfiguration) (path: string) =
    //async {
    //    let outputAssemblyName = makeDllName (Path.GetFileName(path))
    //
    //    let checker = FSharpChecker.Create()
    //
    //    let! errors, exitCode, dynamicAssemblyOption =
    //        checker.CompileToDynamicAssembly(
    //            [| "fsc.exe"
    //               "-o"
    //               outputAssemblyName
    //               "-a"
    //               path
    //               $"--targetprofile:{cfg.TargetProfile}" |],
    //            Some(stdout, stderr)
    //        )
    //
    //    match exitCode, dynamicAssemblyOption with
    //    | 0, Some dynamicAssembly -> return Ok dynamicAssembly
    //    | _ -> return errors |> Error
    //}
    //
    let compileScriptAsync (ctx: CompilerContext) (path: string) =
        async {

            match Path.GetExtension(path) with
            | ".fsx" ->
                let scriptName = Path.GetFileNameWithoutExtension(path)

                // TODO check output path for files with same script name.
                // If found then check the hash with this scripts hash.
                // If same, do nothing.

                let dllName = makeDllName scriptName "TODO"

                let outputAssemblyName = Path.Combine(ctx.Configuration.OutputPath, dllName)

                match! ctx.Checker.Compile(makeArgs ctx path outputAssemblyName) with
                | (_, None) -> return Ok outputAssemblyName
                | (errors, exnOpt) -> return Error(CompilerError.CompilerFailure(errors, exnOpt))
            | ext -> return Error(CompilerError.InvalidFileType ext)
        }

    let compileScript (ctx: CompilerContext) (scriptPath: string) =
        compileScriptAsync ctx scriptPath |> Async.RunSynchronously
