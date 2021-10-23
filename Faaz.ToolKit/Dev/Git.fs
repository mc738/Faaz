namespace Faaz.ToolKit.Dev

open Faaz

[<RequireQualifiedAccess>]
module Git =

    let getLastCommitHash (gitPath: string) (path: string) =
        match Process.run
                  { Name = gitPath
                    Args = "rev-parse HEAD"
                    StartDirectory = (Some path) } with
        | Ok r when r.Length > 0 -> Ok r.Head
        | Ok r -> Ok "Not commit hash found."
        | Error e -> Error e
        
    let clone (gitPath: string) (sourceUrl: string) (path: string) =
            let output, errors =
                Process.execute gitPath $"clone {sourceUrl}" (path |> Some)
                
            match errors.Length = 0 with
            | true -> Ok output
            | false ->
                match errors.[0].StartsWith("Cloning into") with
                | true -> Ok [ "Cloned" ]
                | false -> Error errors

