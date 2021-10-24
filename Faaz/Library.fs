namespace Faaz

open System
open System.IO
open System.Text.Json.Serialization
open Freql.Sqlite

[<AutoOpen>]
module Common =

    let attempt<'R> (fn: unit -> 'R) =
        try
            fn () |> Ok
        with
        | exn -> Error exn.Message

    let tee fn x =
        fn x
        x

    type ScriptConfiguration =
        { Name: string
          SourcePath: string
          BasePath: string
          Args: Map<string, string> }

    type LogEntry =
        { Step: string
          Entry: string
          IsError: bool
          IsWarning: bool }

        static member TableSql() =
            """CREATE TABLE build_logs (
                step TEXT NOT NULL,
                entry TEXT NOT NULL,
                is_error INTEGER NOT NULL,
                is_warning INTEGER NOT NULL
            );"""

    let log (qh: QueryHandler) step entry =
        printfn $"{step} - {entry}"

        qh.Insert(
            "build_logs",
            { Step = step
              Entry = entry
              IsError = false
              IsWarning = false }
        )

    let logError (qh: QueryHandler) step entry =
        Console.ForegroundColor <- ConsoleColor.Red
        printfn $"{step} - {entry}"
        Console.ResetColor()

        qh.Insert(
            "build_logs",
            { Step = step
              Entry = entry
              IsError = true
              IsWarning = false }
        )

    let logWarning (qh: QueryHandler) step entry =
        Console.ForegroundColor <- ConsoleColor.Yellow
        printfn $"{step} - {entry}"
        Console.ResetColor()

        qh.Insert(
            "build_logs",
            { Step = step
              Entry = entry
              IsError = false
              IsWarning = true }
        )

    type ScriptContext =
        { Id: Guid
          Name: string
          BasePath: string
          Data: Map<string, string>
          Writer: QueryHandler }

        static member Create(id, name, basePath, data, initStatements) =
            attempt
                (fun _ ->
                    let qh =
                        QueryHandler.Create(Path.Combine(basePath, "context.db"))

                    { Id = id
                      Name = name
                      BasePath = basePath
                      Data = data
                      Writer = qh })
            |> fun scr ->
                match scr with
                | Ok sc ->
                    match sc.Initialize(initStatements) with
                    | Ok _ -> Ok sc
                    | Error e -> Error $"Failed to initialize script context. Error: {e}"
                | Error e -> Error e

        member sc.Initialize(initSql: string list) =
            attempt
                (fun _ ->
                    [ LogEntry.TableSql() ] @ initSql
                    |> List.map sc.Writer.ExecuteSqlNonQuery
                    |> ignore)

        member sc.Log(step, entry) = log sc.Writer step entry

        member sc.LogError(step, entry) = logError sc.Writer step entry

        member sc.LogWarning(step, entry) = logWarning sc.Writer step entry

        member sc.TryGetValue(key) = sc.Data.TryFind key

        member sc.GetValue(key, defaultValue) =
            sc.Data.TryFind key
            |> Option.defaultValue defaultValue

        member sc.AddValue(key, value) =
            { sc with
                  Data = sc.Data.Add(key, value) }

module Pipelines =
    let bind switchFunction twoTrackInput =
        match twoTrackInput with
        | Ok r -> switchFunction r
        | Error e -> Error e

    let switch f x = f x |> Ok

    /// Combine the results of 2 switch functions in to a tuple.
    /// Useful when you want to keep the result of a previous stage though shouldn't be abused.
    /// Both switch functions will receive the same input (`x`), normally this will be a unit.
    let combine2 switch1 switch2 x =
        match switch1 x with
        | Ok r ->
            match switch2 x with
            | Ok r2 -> Ok(r, r2)
            | Error e -> Error e
        | Error e -> Error e

    /// Bind.
    let (>>=) twoTrackInput switchFunction = bind switchFunction twoTrackInput

    /// Pipe.
    let (>=>) switch1 switch2 x =
        match switch1 x with
        | Ok r -> switch2 r
        | Error e -> Error e

    /// Recover.
    let (>?>) switch1 switch2 x =
        match switch1 x with
        | Ok r -> Ok r
        | Error e -> switch2 x

    /// Pass through.
    let (>->) (switch1: 'a -> Result<'b, 'c>) (passThru: Result<'b, 'c> -> unit) (x: 'a) =
        let r = switch1 x
        passThru r
        r

    /// Infix version of combine.
    /// Combine the results of 2 switch functions that accept the same parameter (normally a unit).
    let (>+>) switch1 switch2 x = combine2 switch1 switch2 x

    let start _ = Ok()

    let startWith<'T> (value: 'T) (run: unit) : Result<'T, string> = switch (fun _ -> value) run

module ScriptHost =
    open System.IO
    open System.Text
    open FSharp.Compiler.Interactive.Shell

    let fsiSession _ =
        // Initialize output and input streams
        let sbOut = new StringBuilder()
        let sbErr = new StringBuilder()
        let inStream = new StringReader("")
        let outStream = new StringWriter(sbOut)
        let errStream = new StringWriter(sbErr)

        // Build command line arguments & start FSI session
        let argv = [| "C:\\fsi.exe" |]

        let allArgs =
            Array.append argv [| "--noninteractive" |]

        let fsiConfig =
            FsiEvaluationSession.GetDefaultConfiguration()

        FsiEvaluationSession.Create(fsiConfig, allArgs, inStream, outStream, errStream)

    let eval<'T> (path: string) (name: string) (fsi: FsiEvaluationSession) =
        try
            match fsi.EvalScriptNonThrowing path with
            | Choice1Of2 _, diagnostics ->
                printfn $"Evaluate script diagnostics: {diagnostics}"

                match fsi.EvalExpressionNonThrowing name with
                | Choice1Of2 r, diagnostics ->
                    printfn $"Evaluate script diagnostics: {diagnostics}"
                    printfn $"Evaluate expression diagnostics: {diagnostics}"
                    //printfn $"Result: {r}"
                    r
                    |> Option.bind (fun v -> v.ReflectionValue |> unbox<'T> |> Ok |> Some)
                    |> Option.defaultValue (Result.Error "No result")
                | Choice2Of2 exn, diagnostics ->
                    printfn $"Error evaluating expression: {exn.Message}"
                    printfn $"Evaluate expression diagnostics: {diagnostics}"
                    Result.Error exn.Message
            | Choice2Of2 exn, diagnostics ->
                printfn $"Error evaluating script: {exn.Message}"
                printfn $"Evaluate script diagnostics: {diagnostics}"
                Result.Error exn.Message
        with
        | exn -> Error $"Unhandled error: {exn.Message}"

    type HostContext =
        { FsiSession: FsiEvaluationSession }

        member hc.Eval<'T>(path, name) = eval<'T> path name hc.FsiSession