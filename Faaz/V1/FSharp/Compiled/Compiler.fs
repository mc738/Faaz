namespace Faaz.V1.FSharp.Compiled

open System
open System.Globalization
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open FsToolbox.Extensions.Strings

module Compiler =

    [<RequireQualifiedAccess>]
    type RecompileOption =
        | Always
        | OnHashChanged


    [<RequireQualifiedAccess>]
    type HashMode =
        | ScriptOnly
        | ScriptAndDependencies

    type HashConfiguration =
        { Type: HashType
          Mode: HashMode }

        static member Default =
            { Type = HashType.SHA256
              Mode = HashMode.ScriptAndDependencies }

    and [<RequireQualifiedAccess>] ChangeDetectionMode =
        | Hash of HashType

        override cdt.ToString() =
            match cdt with
            | Hash hashType -> $"Hash (hash type: {hashType})"

    and [<RequireQualifiedAccess>] HashType =
        | MD5
        | SHA256

        override ht.ToString() =
            match ht with
            | MD5 -> "MD5"
            | SHA256 -> "SHA256"

    type CompilerConfiguration =
        {
            OutputPath: string
            /// <summary>
            /// --targetprofile:[profile]
            /// </summary>
            TargetProfile: string
            RecompileOption: RecompileOption
            HashConfiguration: HashConfiguration
            Options: CompilerOptions
        }

    and CompilerOptions =
        {
            /// <summary>Compiler option: --allsigs</summary>
            AllSign: bool
        }

        static member Default = { AllSign = false }

    type CompilerContext =
        private
            { Checker: FSharpChecker
              Logger: ILogger
              Configuration: CompilerConfiguration }

        static member Create(cfg: CompilerConfiguration, ?logger: ILogger) =
            { Checker = FSharpChecker.Create()
              Logger = logger |> Option.defaultWith (fun _ -> NullLogger.Instance)
              Configuration = cfg }

    type CompilerError =
        | InvalidFileType of Extension: string
        | CompilerFailure of Errors: FSharpDiagnostic array * Exception: exn option

    [<RequireQualifiedAccess>]
    type ScriptDependencyType =
        | Script of string
        | Dll of string
        | Nuget of string

    [<RequireQualifiedAccess>]
    type ChangeDetectionResult =
        | NoChange of PreviousDllPath: string
        | Skipped of NewHash: string
        | HashChanged of NewHash: string

    [<AutoOpen>]
    module private Internal =

        let timestampFormat = "yyyyMMddHHmmss"

        let fileNameHashRegex =
            Regex("^(.*?)___(?<hash>(.*?))_(?<timestamp>(.*?)).dll$", RegexOptions.Singleline ||| RegexOptions.Compiled)

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

        let makeDllName (scriptName: string) (hash: string) =
            $"{scriptName}___{hash}_{DateTime.UtcNow:timestampFormat}.dll"

        let getDependencies (scriptPath: string) =
            File.ReadLines(scriptPath)
            |> Seq.choose (fun line ->
                //match line with
                try
                    match line with
                    | _ when nugetRegex.IsMatch(line) ->
                        //let matches =
                        nugetRegex.Matches(line).[0].Groups["path"].Value
                        |> _.ToOption()
                        |> Option.map ScriptDependencyType.Nuget
                    | _ when referenceRegex.IsMatch(line) ->
                        referenceRegex.Matches(line).[0].Groups["path"].Value
                        |> _.ToOption()
                        |> Option.map ScriptDependencyType.Dll
                    | _ when loadRegex.IsMatch(line) ->
                        loadRegex.Matches(line).[0].Groups["path"].Value
                        |> _.ToOption()
                        |> Option.map ScriptDependencyType.Script
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
        //|> fun hashBytes -> BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant()

        let bytesToString (bytes: byte array) =
            BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant()


        //hasher.ComputeHashAsync
        //
        //use crypto = new CryptoStream(fs, hasher, CryptoStreamMode.Read)
        //
        ////do! File.ReadAllBytesAsync(path) |> hasher.ComputeHashAsync
        //
        //File.ReadAllBytes(path)

        let p (path: string) = Path.IsPathFullyQualified(path)

        //let p (path: string) = Path.getf

        let createHasher (hashType: HashType) =
            match hashType with
            | HashType.MD5 -> MD5.Create() :> HashAlgorithm
            | HashType.SHA256 -> SHA256.Create() :> HashAlgorithm

        let generateHash (ctx: CompilerContext) (hasher: HashAlgorithm) (scriptPath: string) =
            ctx.Logger.LogTrace("Generating hash for script {ScriptPath} and dependencies.", scriptPath)

            let rec traverse (path: string) =

                let fileDirectory = Path.GetDirectoryName(path)

                let getDependencyPath (path: string) = Path.GetFullPath(path, fileDirectory)

                // Get The file hash
                let fileHash = hashFile hasher path

                let dependencyHashes =
                    getDependencies path
                    |> Seq.map (function
                        | ScriptDependencyType.Script scriptPath -> getDependencyPath scriptPath |> traverse
                        | ScriptDependencyType.Dll dllPath -> hashFile hasher dllPath
                        | ScriptDependencyType.Nuget nugetPath ->
                            // TODO nuget not supported yet - for now just generate a random guid
                            Guid.NewGuid().ToByteArray())

                ctx.Logger.LogTrace("Hashing file {Path} and combining with any dependents", path)

                seq {
                    fileHash
                    yield! dependencyHashes
                }
                |> Array.concat

            traverse scriptPath |> hasher.ComputeHash |> bytesToString

        /// Extracts the hash and timestamp from the provided filename if it matches the expected format.
        /// The filename is expected to follow the pattern: `<name>___<hash>_<yyyyMMddHHmmss>.dll`.
        /// This function extracts the `hash` and `timestamp` from the filename and returns a record
        /// containing the full file path, the hash, and the timestamp. If the timestamp cannot be parsed,
        /// the file's creation time in UTC is used as a fallback.
        ///
        /// If the filename does not match the expected format, the function returns `None`.
        ///
        /// Parameters:
        ///   fileInfo: FileInfo representing the file to extract metadata from.
        ///
        /// Returns:
        ///   Option<{| FilePath: string; Hash: string; Timestamp: DateTime |}>:
        ///     Returns `Some` with the extracted hash, timestamp, and file path if parsing is successful,
        ///     or `None` if the filename format does not match.
        let ``get previous hash and timestamp from file name`` (fileInfo: FileInfo) =
            let m = fileNameHashRegex.Match(fileInfo.Name)

            if m.Success then
                {| FilePath = fileInfo.FullName
                   Hash = m.Groups["hash"].Value
                   Timestamp =
                    match
                        DateTime.TryParseExact(
                            m.Groups["timestamp"].Value,
                            timestampFormat,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal
                        )
                    with
                    | true, v -> v
                    | false, _ ->
                        // If the previous timestamp can not be exacted from the file now,
                        // use the file creation time.
                        // This is less stable but will do for this cirumstance.
                        fileInfo.CreationTimeUtc |}
                |> Some
            else
                None

        let ``get previous dll hash`` (directoryPath: string) (scriptName: string) =
            DirectoryInfo(directoryPath).EnumerateFiles($"{Path.GetFileNameWithoutExtension(scriptName)}___*.dll")
            |> Seq.choose ``get previous hash and timestamp from file name``
            |> Seq.sortByDescending (fun dll -> dll.Timestamp)
            |> Seq.tryHead
            |> Option.map (fun r -> r.Hash, r.FilePath)

        let ``check for changes`` (ctx: CompilerContext) (scriptPath: string) (forceRecompile: bool) =
            let hasher = createHasher ctx.Configuration.HashConfiguration.Type

            let newHash =
                match ctx.Configuration.HashConfiguration.Mode with
                | HashMode.ScriptOnly -> hashFile hasher scriptPath |> bytesToString
                | HashMode.ScriptAndDependencies -> generateHash ctx hasher scriptPath

            match ctx.Configuration.RecompileOption, forceRecompile with
            | RecompileOption.Always, _
            | _, true ->
                ctx.Logger.LogTrace("Recompile option set to {RecompileOption}, skipping checks.", "Always")

                ChangeDetectionResult.Skipped newHash
            | RecompileOption.OnHashChanged, false ->
                ctx.Logger.LogTrace("Recompile option set to {RecompileOption}, skipping checks.", "OnHashChanged")

                match ``get previous dll hash`` ctx.Configuration.OutputPath scriptPath with
                | None -> ChangeDetectionResult.HashChanged newHash
                | Some(previousHash, filePath) ->
                    if newHash.Equals(previousHash, StringComparison.OrdinalIgnoreCase) |> not then
                        ChangeDetectionResult.HashChanged newHash
                    else
                        ChangeDetectionResult.NoChange filePath
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
    /// Asynchronously compiles an F# script file (.fsx) into a DLL.
    ///
    /// This function takes a `CompilerContext` and a path to a script file, then
    /// performs the following steps:
    /// - Validates that the file extension matches .fsx.
    /// - Checks for changes to the script by comparing hashes of previous compiled versions.
    /// - If no changes are detected, skips recompilation and returns the path to the previously compiled DLL.
    /// - If changes are detected, generates a new DLL with the script, using the specified output path in the configuration.
    ///
    /// Parameters:
    /// - `ctx`: The context containing the F# compiler, logging infrastructure, and other configuration settings.
    /// - `scriptPath`: The path to the F# script file to be compiled.
    ///
    /// Returns:
    /// - An asynchronous computation that, upon completion, will yield:
    ///   - `Ok dllPath` if the script compilation is successful or if no changes are detected, where `dllPath` is the path to the resulting DLL.
    ///   - `Error` with a reason if the script compilation fails or the file type is invalid.
    ///
    /// Notes:
    /// - Files that do not have the .fsx extension are rejected as invalid file types.
    /// - If the script has not been modified and the hash matches a previously compiled version, recompilation is skipped.
    let compileScriptAsync (ctx: CompilerContext) (scriptPath: string) =
        async {

            match Path.GetExtension(scriptPath) with
            | ".fsx" ->
                let scriptName = Path.GetFileNameWithoutExtension(scriptPath)

                // TODO check output path for files with same script name.
                // If found then check the hash with this scripts hash.
                // If same, do nothing.

                match ``check for changes`` ctx scriptPath false with
                | ChangeDetectionResult.NoChange previousDllPath ->
                    ctx.Logger.LogTrace("No changes detected to script {ScriptPath}. Skipping compilation.", scriptPath)
                    return Ok previousDllPath
                | ChangeDetectionResult.Skipped newHash
                | ChangeDetectionResult.HashChanged newHash ->
                    ctx.Logger.LogTrace("Changes detected to script {ScriptPath}. Compiling script.", scriptPath)


                    let dllName = makeDllName scriptName newHash

                    let outputAssemblyName = Path.Combine(ctx.Configuration.OutputPath, dllName)

                    match! ctx.Checker.Compile(makeArgs ctx scriptPath outputAssemblyName) with
                    | (_, None) -> return Ok outputAssemblyName
                    | (errors, exnOpt) -> return Error(CompilerError.CompilerFailure(errors, exnOpt))
            | ext -> return Error(CompilerError.InvalidFileType ext)
        }

    let compileScript (ctx: CompilerContext) (scriptPath: string) =
        compileScriptAsync ctx scriptPath |> Async.RunSynchronously
