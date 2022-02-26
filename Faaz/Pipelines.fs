namespace Faaz

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
