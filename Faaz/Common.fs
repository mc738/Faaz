namespace Faaz


[<AutoOpen>]
module Common =
    open System
    open System.IO
    open Freql.Sqlite

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

    let log (context: SqliteContext) step entry =
        printfn $"{step} - {entry}"

        context.Insert(
            "build_logs",
            { Step = step
              Entry = entry
              IsError = false
              IsWarning = false }
        )

    let logError (context: SqliteContext) step entry =
        Console.ForegroundColor <- ConsoleColor.Red
        printfn $"{step} - {entry}"
        Console.ResetColor()

        context.Insert(
            "build_logs",
            { Step = step
              Entry = entry
              IsError = true
              IsWarning = false }
        )

    let logWarning (qh: SqliteContext) step entry =
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
          Writer: SqliteContext }

        static member Create(id, name, basePath, data, initStatements) =
            attempt
                (fun _ ->
                    let context =
                        SqliteContext.Create(Path.Combine(basePath, "context.db"))

                    { Id = id
                      Name = name
                      BasePath = basePath
                      Data = data
                      Writer = context })
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